using MHARS.Web.Models;

namespace MHARS.Web.Services;

public record VerificationResult(
    VerificationStatus Status,
    string Note);

public interface IAlertVerificationService
{
    Task<VerificationResult> VerifySourceAsync(
        string? sourceUrl,
        string? district,
        string? title,
        string? message,
        CancellationToken ct = default);
}