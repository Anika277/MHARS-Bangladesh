using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MHARS.Web.Models;

namespace MHARS.Web.Services;

/// <summary>
/// Verifies the official source an Admin cites for a flood alert.
///
///   Check 1  Trusted domain   – ffwc.gov.bd, bmd.gov.bd, bwdb.gov.bd, ddm.gov.bd, reliefweb.int (appsettings)
///   Check 2  Source is live   – the page opens now (HTTP 200 over HTTPS)
///   Check 3  District match   – the page's MAIN content mentions the alert's district (English or
///                               Bangla). Menus/headers/footers are ignored, because many government
///                               sites list every division in their navigation.
///
///   1 + 2 + 3  → Verified          ("Verified by official source")
///   1 + 2      → SourceReachable   ("Official source" – page could not be matched to the district)
///   any fail   → Rejected
///   no link    → Unverified
///
/// Honest limit (say this in the viva): MHARS confirms the alert is BACKED by a live official
/// page about that district. It does not measure water levels itself — that is FFWC's job.
/// </summary>
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
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return new(VerificationStatus.Unverified, "No official source link was provided.");

        if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var uri))
            return new(VerificationStatus.Rejected, "The source link is not a valid web address.");

        if (uri.Scheme != Uri.UriSchemeHttps)
            return new(VerificationStatus.Rejected, "Only secure (https://) official sources are accepted.");

        // ---- Check 1: trusted domain ----
        var host = uri.Host.ToLowerInvariant();
        if (!IsTrusted(host))
            return new(VerificationStatus.Rejected, $"'{host}' is not a recognised official source.");

        // ---- Check 2: source is live ----
        string? pageText;
        string mediaType;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd("MHARS-Verifier/1.0 (AUST CSE 3200 academic project)");
            req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.5");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opt.TimeoutSeconds));

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            // The link may redirect (e.g. www.bmd.gov.bd → server6.bmd.gov.bd). Wherever it LANDS
            // must also be an official https site — otherwise an official link could bounce anywhere.
            var landed = res.RequestMessage?.RequestUri ?? uri;
            if (landed.Scheme != Uri.UriSchemeHttps || !IsTrusted(landed.Host))
                return new(VerificationStatus.Rejected, $"The link redirected to '{landed.Host}', which is not an official source.");

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Verify {Url} -> HTTP {Status}", uri, (int)res.StatusCode);
                return new(VerificationStatus.Rejected, $"The official page did not open (HTTP {(int)res.StatusCode}).");
            }

            mediaType = res.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            pageText = IsReadableText(mediaType)
                ? await ReadUpToAsync(res, _opt.ContentCheckMaxBytes, cts.Token)
                : null;
        }
        catch (TaskCanceledException)
        {
            return new(VerificationStatus.Rejected, "The official page took too long to respond.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Source fetch failed for {Url}", uri);
            return new(VerificationStatus.Rejected, "The official page could not be reached.");
        }

        // ---- Check 3: page mentions this district ----
        var names = DistrictNameAliases.For(district);
        if (names.Length == 0)
            return new(VerificationStatus.SourceReachable, $"Official source {host} is live. No district to match.");

        if (pageText is null)
            return new(VerificationStatus.SourceReachable,
                $"Official source {host} is live. Its content ({Describe(mediaType)}) could not be read automatically to match {district}.");

        var text = ToPlainText(pageText);
        if (text.Length < 200)
            return new(VerificationStatus.SourceReachable,
                $"Official source {host} is live. The page has little readable text (content loads by script or is in an image/PDF), so {district} could not be matched automatically.");

        var found = names.FirstOrDefault(n => Contains(text, n));
        return found is not null
            ? new(VerificationStatus.Verified, $"Official source {host} is live and mentions \"{found}\".")
            : new(VerificationStatus.SourceReachable,
                $"Official source {host} is live, but the page does not mention {district}. Check that the link points to the right bulletin.");
    }

    // ------------------------------------------------------------------ helpers

    private bool IsTrusted(string host)
    {
        host = host.ToLowerInvariant();
        return _opt.AllowedDomains.Any(d => host == d || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReadableText(string mediaType) =>
        mediaType.StartsWith("text/") || mediaType.Contains("html") || mediaType.Contains("xml") || mediaType.Contains("json");

    private static string Describe(string mediaType) =>
        mediaType.Contains("pdf") ? "a PDF" : string.IsNullOrEmpty(mediaType) ? "unknown type" : mediaType;

    /// <summary>Reads at most maxBytes of the body as UTF-8 (enough for any bulletin page).</summary>
    private static async Task<string> ReadUpToAsync(HttpResponseMessage res, int maxBytes, CancellationToken ct)
    {
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[Math.Max(maxBytes, 8192)];
        int total = 0, read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), ct)) > 0)
            total += read;
        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static readonly Regex ScriptOrStyle = new(@"<(script|style|noscript)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Menus, headers, footers, sidebars and drop-downs list EVERY division/district on many
    // government sites. A name found there says nothing about this bulletin, so we remove them
    // and only match the page's main content.
    private static readonly Regex SiteChrome = new(
        @"<(nav|header|footer|aside|select)\b[^>]*>.*?</\1>" +
        @"|<ul\b[^>]*class\s*=\s*[""'][^""']*\b(menu|nav|navbar|dropdown)[^""']*[""'][^>]*>.*?</ul>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Tags = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>HTML → visible text, with Bangla characters normalised so "কুড়িগ্রাম" always matches.</summary>
    private static string ToPlainText(string html)
    {
        var s = ScriptOrStyle.Replace(html, " ");
        s = SiteChrome.Replace(s, " ");
        s = Tags.Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = Spaces.Replace(s, " ").Trim();
        return s.Normalize(NormalizationForm.FormC);
    }

    private static bool Contains(string text, string name) =>
        text.Contains(name.Normalize(NormalizationForm.FormC), StringComparison.OrdinalIgnoreCase);
}
