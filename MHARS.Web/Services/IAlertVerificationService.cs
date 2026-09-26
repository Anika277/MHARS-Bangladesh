using MHARS.Web.Models;

namespace MHARS.Web.Services;

public record VerificationResult(
    VerificationStatus Status,
    string Note);

public interface IAlertVerificationService
{
    Task<VerificationResult> VerifySourceAsync(
        string? sourceUrl,
        CancellationToken ct = default);
}