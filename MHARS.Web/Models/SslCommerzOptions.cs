namespace MHARS.Web.Models;

/// <summary>
/// Bound from the "SslCommerz" configuration section.
/// StoreId and StorePassword are SECRETS: put them in .env (local) or the host's
/// environment variables (deployed) — never in appsettings.json, which is committed to GitHub.
///   SslCommerz__StoreId=yourstore123
///   SslCommerz__StorePassword=yourstore123@ssl
/// </summary>
public class SslCommerzOptions
{
    public string StoreId { get; set; } = string.Empty;
    public string StorePassword { get; set; } = string.Empty;

    /// <summary>true = sandbox (fake money). Must stay true for this course project.</summary>
    public bool IsSandbox { get; set; } = true;

    /// <summary>
    /// Show "test mode" notices (banner, test-card hint, DEMO watermark on receipts).
    /// Off by default for a clean look. Turn on in appsettings.json if the site is
    /// public and you want visitors to know no real money is collected.
    /// </summary>
    public bool ShowTestModeNotice { get; set; } = false;

    /// <summary>
    /// Optional. Public base URL used to build success/fail/cancel/IPN callback URLs,
    /// e.g. "http://mhars.runasp.net". Leave empty to use the URL of the current request.
    /// </summary>
    public string? CallbackBaseUrl { get; set; }

    public string GatewayBaseUrl => IsSandbox
        ? "https://sandbox.sslcommerz.com"
        : "https://securepay.sslcommerz.com";

    public string InitiateUrl => $"{GatewayBaseUrl}/gwprocess/v4/api.php";
    public string ValidationUrl => $"{GatewayBaseUrl}/validator/api/validationserverAPI.php";
}
