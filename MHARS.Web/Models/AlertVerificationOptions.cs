namespace MHARS.Web.Models;

public class AlertVerificationOptions
{
    public List<string> AllowedDomains { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 12;
    public int MaxResponseBytes { get; set; } = 8192;
}