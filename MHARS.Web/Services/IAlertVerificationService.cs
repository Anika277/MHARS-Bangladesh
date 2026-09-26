using MHARS.Web.Models;

namespace MHARS.Web.Services;

public record VerificationResult(
    VerificationStatus Status,
    string Note);

public interface IAlertVerificationService
{
    /// <summary>
    /// Checks the official source cited for an alert:
    ///   1. the link is on a trusted government / humanitarian domain,
    ///   2. the page opens right now, and
    ///   3. the page actually mentions the alert's district (English or Bangla).
    /// All 3 → Verified.  1 + 2 only → SourceReachable ("Official source").
    /// </summary>
    Task<VerificationResult> VerifySourceAsync(
        string? sourceUrl,
        string? district,
        CancellationToken ct = default);
}