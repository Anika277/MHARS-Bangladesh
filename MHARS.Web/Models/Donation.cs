using System.ComponentModel.DataAnnotations;

namespace MHARS.Web.Models;

public enum DonationStatus
{
    Pending = 0,    // row created, donor sent to SSLCommerz, no confirmed result yet
    Paid = 1,       // confirmed by the SSLCommerz Validation API — the ONLY status counted in totals
    Failed = 2,
    Cancelled = 3
}

/// <summary>
/// One payment attempt. A row is written BEFORE the donor is redirected to the gateway
/// (Status = Pending) and is only flipped to Paid after our server re-checks the payment
/// with SSLCommerz's Validation API. The browser is never trusted to say "I paid".
/// </summary>
public class Donation
{
    public int Id { get; set; }

    public int CampaignId { get; set; }
    public DonationCampaign? Campaign { get; set; }

    [StringLength(50)]
    public string DonorName { get; set; } = string.Empty;

    [StringLength(100)]
    public string DonorEmail { get; set; } = string.Empty;

    [StringLength(20)]
    public string DonorPhone { get; set; } = string.Empty;

    /// <summary>If true, the public tracker shows "Anonymous" instead of the name.</summary>
    public bool IsAnonymous { get; set; }

    public decimal Amount { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "BDT";

    /// <summary>Our transaction id, sent to SSLCommerz as tran_id (max 30 chars). Unique.</summary>
    [StringLength(30)]
    public string TranId { get; set; } = string.Empty;

    public DonationStatus Status { get; set; } = DonationStatus.Pending;

    // ---- Filled in from the SSLCommerz Validation API after payment ----
    [StringLength(50)]
    public string? ValId { get; set; }

    [StringLength(80)]
    public string? BankTranId { get; set; }

    [StringLength(50)]
    public string? CardType { get; set; }

    [StringLength(255)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }

    /// <summary>
    /// Unguessable id used in the receipt URL (/Donations/Receipt/{token}).
    /// Using the int Id there would let anyone read other donors' receipts by counting 1, 2, 3...
    /// </summary>
    public Guid ReceiptToken { get; set; } = Guid.NewGuid();

    /// <summary>Human-friendly receipt number, e.g. MHARS-DN-000042.</summary>
    public string ReceiptNumber => $"MHARS-DN-{Id:D6}";
}
