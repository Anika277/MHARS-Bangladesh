using MHARS.Web.Models;

namespace MHARS.Web.Services;

public record SslInitResult(bool Success, string? GatewayUrl, string? Error);

public record SslValidationResult(
    bool IsValid,
    string Status,
    string? TranId,
    decimal? Amount,
    string? Currency,
    string? BankTranId,
    string? CardType,
    string? Error);

public interface ISslCommerzService
{
    /// <summary>Step 1: ask SSLCommerz to open a payment session; returns the page to send the donor to.</summary>
    Task<SslInitResult> InitiateAsync(Donation donation, string campaignTitle, string city,
        string successUrl, string failUrl, string cancelUrl, string ipnUrl,
        CancellationToken ct = default);

    /// <summary>Step 3: server-to-server check that a val_id really is a completed payment.</summary>
    Task<SslValidationResult> ValidateAsync(string valId, CancellationToken ct = default);
}
