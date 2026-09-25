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
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return new(VerificationStatus.Unverified,
                       "No source URL supplied.");

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return new(VerificationStatus.Rejected,
                       "Source URL is not a valid absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttps)
            return new(VerificationStatus.Rejected,
                       "Only HTTPS sources are accepted.");

        var host = uri.Host.ToLowerInvariant();
        var allowed = _opt.AllowedDomains.Any(d =>
            host == d ||
            host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
            return new(VerificationStatus.Rejected,
                       $"Domain '{host}' is not on the government allowlist.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd(
                "MHARS-Verifier/1.0 (AUST CSE 3200 academic project)");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opt.TimeoutSeconds));

            using var res = await _http.SendAsync(
                req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Verify {Url} -> HTTP {Status}",
                                   uri, (int)res.StatusCode);
                return new(VerificationStatus.Rejected,
                           $"Source returned HTTP {(int)res.StatusCode}.");
            }

            await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[_opt.MaxResponseBytes];
            _ = await stream.ReadAsync(buffer, cts.Token);

            return new(VerificationStatus.SourceReachable,
                       $"Source reachable at {uri.Host} ({res.StatusCode}).");
        }
        catch (TaskCanceledException)
        {
            return new(VerificationStatus.Rejected,
                       "Source fetch timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Source fetch failed for {Url}", uri);
            return new(VerificationStatus.Rejected,
                       "Source could not be reached.");
        }
    }
}