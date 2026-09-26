using System.ComponentModel.DataAnnotations;

namespace MHARS.Web.Models;

public enum HazardType
{
    Flood = 1,
    Earthquake = 2
}

public enum SeverityLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum VerificationStatus
{
    Unverified = 0,       // no official source link
    SourceReachable = 1,  // official source is live, but could not be matched to the district ("Official source")
    Rejected = 2,         // link failed: not official, dead, or not https
    Verified = 3          // official source is live AND mentions this district (strongest)
    // Stored as int, so adding "Verified = 3" needs NO database migration.
}

public class Alert
{
    public int Id { get; set; }

    [Required]
    public HazardType HazardType { get; set; }

    [Required]
    [StringLength(50)]
    public string District { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public SeverityLevel Severity { get; set; }

    [StringLength(200)]
    public string? SourceReference { get; set; }

    [StringLength(500)]
    [Url]
    public string? SourceUrl { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Unverified;

    [StringLength(300)]
    public string? VerificationNote { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? IssuedBy { get; set; }
}