using System.ComponentModel.DataAnnotations;

namespace MHARS.Web.Models;

/// <summary>
/// A relief fund an Admin opens for a specific disaster (e.g. "Sylhet Flood Relief 2026").
/// Donations always belong to exactly one campaign; the tracker sums them per campaign.
/// District is plain text, matching the Alert/Shelter design (no District lookup table).
/// A null District means a nationwide campaign.
/// </summary>
public class DonationCampaign
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public HazardType? HazardType { get; set; }

    [StringLength(50)]
    public string? District { get; set; }

    [Range(1000, 100000000, ErrorMessage = "Goal must be between ৳1,000 and ৳10,00,00,000.")]
    [Display(Name = "Goal amount (BDT)")]
    public decimal GoalAmount { get; set; }

    [Display(Name = "Accepting donations")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Donation> Donations { get; set; } = [];
}
