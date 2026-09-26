namespace MHARS.Web.Models;

public class AlertVerificationOptions
{
    public List<string> AllowedDomains { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 12;
    public int MaxResponseBytes { get; set; } = 8192;

    /// <summary>How much of the official page to read when matching the district name (default 500 KB).</summary>
    public int ContentCheckMaxBytes { get; set; } = 500_000;
}