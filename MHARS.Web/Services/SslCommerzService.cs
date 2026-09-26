using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MHARS.Web.Models;

namespace MHARS.Web.Services;

/// <summary>
/// Talks to SSLCommerz v4 over plain HttpClient (same pattern as the USGS service —
/// no third-party SDK, so every line can be explained in the viva).
///
/// Flow:
///   1. InitiateAsync  → POST form to /gwprocess/v4/api.php → get GatewayPageURL
///   2. Donor pays on SSLCommerz's page (card / bKash / Nagad / bank — sandbox = fake money)
///   3. SSLCommerz sends the donor's browser back to our success/fail/cancel URL (form POST),
///      and separately calls our IPN URL server-to-server.
///   4. ValidateAsync  → GET /validator/api/validationserverAPI.php?val_id=... → confirm
/// </summary>
public class SslCommerzService(
    HttpClient http,
    IOptions<SslCommerzOptions> options,
    ILogger<SslCommerzService> logger) : ISslCommerzService
{
    private readonly SslCommerzOptions _opt = options.Value;

    public async Task<SslInitResult> InitiateAsync(Donation donation, string campaignTitle, string city,
        string successUrl, string failUrl, string cancelUrl, string ipnUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.StoreId) || string.IsNullOrWhiteSpace(_opt.StorePassword))
            return new SslInitResult(false, null,
                "Payment gateway is not configured (missing SslCommerz StoreId / StorePassword).");

        var form = new Dictionary<string, string>
        {
            ["store_id"] = _opt.StoreId,
            ["store_passwd"] = _opt.StorePassword,
            ["total_amount"] = donation.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = donation.Currency,
            ["tran_id"] = donation.TranId,
            ["success_url"] = successUrl,
            ["fail_url"] = failUrl,
            ["cancel_url"] = cancelUrl,
            ["ipn_url"] = ipnUrl,

            ["cus_name"] = Truncate(donation.DonorName, 50),
            ["cus_email"] = Truncate(donation.DonorEmail, 50),
            ["cus_phone"] = Truncate(donation.DonorPhone, 20),
            ["cus_add1"] = Truncate(city, 50),       // donation → no postal address is collected
            ["cus_city"] = Truncate(city, 50),
            ["cus_postcode"] = "0000",
            ["cus_country"] = "Bangladesh",

            ["shipping_method"] = "NO",               // nothing is shipped
            ["num_of_item"] = "1",
            ["product_name"] = Truncate($"Donation - {campaignTitle}", 255),
            ["product_category"] = "Donation",
            ["product_profile"] = "non-physical-goods"
        };

        try
        {
            using var response = await http.PostAsync(_opt.InitiateUrl, new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = GetString(root, "status");
            var gatewayUrl = GetString(root, "GatewayPageURL");

            if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(gatewayUrl))
            {
                return new SslInitResult(true, gatewayUrl, null);
            }

            var reason = GetString(root, "failedreason") ?? "Unknown error from payment gateway.";
            logger.LogWarning("SSLCommerz init failed for {TranId}: {Reason}", donation.TranId, reason);
            return new SslInitResult(false, null, reason);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "SSLCommerz init request failed for {TranId}", donation.TranId);
            return new SslInitResult(false, null, "Could not reach the payment gateway. Please try again.");
        }
    }

    public async Task<SslValidationResult> ValidateAsync(string valId, CancellationToken ct = default)
    {
        var url = $"{_opt.ValidationUrl}" +
                  $"?val_id={Uri.EscapeDataString(valId)}" +
                  $"&store_id={Uri.EscapeDataString(_opt.StoreId)}" +
                  $"&store_passwd={Uri.EscapeDataString(_opt.StorePassword)}" +
                  "&format=json";
        try
        {
            var body = await http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var status = GetString(root, "status") ?? "UNKNOWN";

            // VALID = first successful check; VALIDATED = already checked before (e.g. by the IPN). Both mean paid.
            var ok = status is "VALID" or "VALIDATED";

            // currency_amount / currency_type = what WE sent; amount / currency = settled amount.
            // We only ever charge BDT, but prefer the "as sent" fields when present.
            var amountText = GetString(root, "currency_amount") ?? GetString(root, "amount");
            decimal? amount = decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
                ? a : null;

            return new SslValidationResult(
                IsValid: ok,
                Status: status,
                TranId: GetString(root, "tran_id"),
                Amount: amount,
                Currency: GetString(root, "currency_type") ?? GetString(root, "currency"),
                BankTranId: GetString(root, "bank_tran_id"),
                CardType: GetString(root, "card_type"),
                Error: ok ? null : $"Gateway reported status '{status}'.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "SSLCommerz validation call failed for val_id {ValId}", valId);
            // Network problem ≠ failed payment. Caller keeps the donation Pending.
            return new SslValidationResult(false, "UNREACHABLE", null, null, null, null, null,
                "Could not reach the payment gateway to confirm the payment.");
        }
    }

    // SSLCommerz sometimes returns numbers as strings and sometimes as numbers — read both.
    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el)
            ? el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                _ => null
            }
            : null;

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
