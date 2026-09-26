using System.ComponentModel.DataAnnotations;

namespace MHARS.Web.Models.ViewModels;

/// <summary>What the donor types into the donation form.</summary>
public class DonateViewModel
{
    public int CampaignId { get; set; }

    // Display-only (re-loaded from DB on POST, never trusted from the form)
    public string? CampaignTitle { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Full name")]
    public string DonorName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    [Display(Name = "Email (receipt reference)")]
    public string DonorEmail { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [RegularExpression(@"^(\+?880|0)1[3-9]\d{8}$", ErrorMessage = "Enter a valid Bangladeshi mobile number, e.g. 01712345678.")]
    [Display(Name = "Mobile number")]
    public string DonorPhone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "District")]
    public string City { get; set; } = "Dhaka";

    // SSLCommerz accepts 10.00 – 500000.00 BDT per transaction.
    [Range(typeof(decimal), "10", "500000", ErrorMessage = "Amount must be between ৳10 and ৳5,00,000.")]
    [Display(Name = "Amount (BDT)")]
    public decimal Amount { get; set; } = 500;

    [Display(Name = "Hide my name on the public tracker")]
    public bool IsAnonymous { get; set; }
}

/// <summary>One row of the public tracker.</summary>
public class CampaignProgress
{
    public required DonationCampaign Campaign { get; init; }
    public decimal Raised { get; init; }
    public int DonorCount { get; init; }

    public int PercentOfGoal => Campaign.GoalAmount <= 0
        ? 0
        : (int)Math.Min(100, Math.Round(Raised / Campaign.GoalAmount * 100));
}

public class RecentDonationRow
{
    public string DisplayName { get; init; } = "Anonymous";
    public decimal Amount { get; init; }
    public string CampaignTitle { get; init; } = string.Empty;
    public DateTime PaidAtUtc { get; init; }
}

public class DonationTrackerViewModel
{
    public decimal TotalRaised { get; init; }
    public int TotalDonations { get; init; }
    public int ActiveCampaigns { get; init; }
    public List<CampaignProgress> Campaigns { get; init; } = [];
    public List<RecentDonationRow> Recent { get; init; } = [];
    public bool ShowTestNotice { get; init; }
}
