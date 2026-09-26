using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MHARS.Web.Models;

namespace MHARS.Web.Services;

public class AlertVerificationService : IAlertVerificationService
{
    private readonly HttpClient _http;
    private readonly AlertVerificationOptions _opt;
    private readonly ILogger<AlertVerificationService> _logger;

    public AlertVerificationService(
        IHttpClientFactory factory,
        IOptions<AlertVerificationOptions> opt,
        ILogger<AlertVerificationService> logger)
    {
        _http = factory.CreateClient("alert-verification");
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<VerificationResult> VerifySourceAsync(
        string? sourceUrl,
        string? district,
        string? title,
        string? message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return new(VerificationStatus.Unverified, "No source URL supplied.");

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return new(VerificationStatus.Rejected, "Source URL is not a valid absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttps)
            return new(VerificationStatus.Rejected, "Only HTTPS sources are accepted.");

        var host = uri.Host.ToLowerInvariant();
        var allowed = _opt.AllowedDomains.Any(d =>
            host == d || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
            return new(VerificationStatus.Rejected,
                       $"Domain '{host}' is not on the government allowlist.");

        // Reject citing the bare homepage of a domain — too generic to be a real bulletin.
        if (uri.AbsolutePath == "/" || string.IsNullOrEmpty(uri.AbsolutePath))
            return new(VerificationStatus.Rejected,
                       $"Cite a specific bulletin page, not the {host} homepage.");

        string pageText;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd(
                "MHARS-Verifier/1.0 (AUST CSE 3200 academic project)");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opt.TimeoutSeconds));

            using var res = await _http.SendAsync(req, cts.Token);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Verify {Url} -> HTTP {Status}", uri, (int)res.StatusCode);
                return new(VerificationStatus.Rejected,
                           $"Source returned HTTP {(int)res.StatusCode}.");
            }

            var bytes = await res.Content.ReadAsByteArrayAsync(cts.Token);
            if (bytes.Length > _opt.MaxResponseBytes * 8)
                bytes = bytes[..(_opt.MaxResponseBytes * 8)];

            pageText = Encoding.UTF8.GetString(bytes);
        }
        catch (TaskCanceledException)
        {
            return new(VerificationStatus.Rejected, "Source fetch timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Source fetch failed for {Url}", uri);
            return new(VerificationStatus.Rejected, "Source could not be reached.");
        }

        // Strip HTML tags to get plain text.
        var plain = Regex.Replace(pageText, "<[^>]+>", " ");
        plain = WebUtility.HtmlDecode(plain);
        plain = Regex.Replace(plain, @"\s+", " ").ToLowerInvariant();

        // District check — required.
        if (!string.IsNullOrWhiteSpace(district) && district != "All")
        {
            var d = district.ToLowerInvariant();
            if (!plain.Contains(d))
            {
                return new(VerificationStatus.Rejected,
                           $"Source page does not mention district '{district}'. " +
                           "Cite the specific bulletin for this district.");
            }
        }

        // Number check — optional but catches fabricated figures.
        var numericMatches = Regex.Matches(message ?? "", @"\d+(?:\.\d+)?");
        if (numericMatches.Count > 0)
        {
            var numbersFound = 0;
            var numbersTotal = 0;
            foreach (Match m in numericMatches)
            {
                numbersTotal++;
                if (plain.Contains(m.Value))
                    numbersFound++;
            }

            if (numbersTotal > 0 && numbersFound == 0)
            {
                return new(VerificationStatus.Rejected,
                           "Source page contains none of the figures cited in the alert " +
                           "(water level / magnitude). Content does not match.");
            }
        }

        return new(VerificationStatus.SourceReachable,
                   $"Cited page on {host} mentions district '{district}' and matches " +
                   "the figures in the alert. Content check passed (not cryptographic).");
    }
}