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

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? IssuedBy { get; set; }
}
