using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MHARS.Web.Data;
using MHARS.Web.Models;
using MHARS.Web.Models.ViewModels;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

/// <summary>
/// Relief-fund donations: public tracker, donation form, SSLCommerz callbacks, receipts, admin view.
///
/// Golden rule of this controller: a donation becomes "Paid" ONLY inside ConfirmAsync(),
/// after OUR server asks SSLCommerz's Validation API. Anything the browser posts to us
/// (status=VALID, amount=...) is treated as a hint, never as proof.
/// </summary>
public class DonationsController(
    ApplicationDbContext db,
    ISslCommerzService ssl,
    IOptions<SslCommerzOptions> sslOptions,
    ILogger<DonationsController> logger) : Controller
{
    private readonly SslCommerzOptions _opt = sslOptions.Value;

    // =====================================================================
    //  PUBLIC: tracker
    // =====================================================================

    // GET /Donations
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var paid = db.Donations.AsNoTracking().Where(d => d.Status == DonationStatus.Paid);

        // One SQL GROUP BY: raised + donor count per campaign (only Paid rows are counted).
        var totals = await paid
            .GroupBy(d => d.CampaignId)
            .Select(g => new { CampaignId = g.Key, Raised = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var campaigns = await db.DonationCampaigns.AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var recent = await paid
            .OrderByDescending(d => d.PaidAtUtc)
            .Take(10)
            .Select(d => new RecentDonationRow
            {
                DisplayName = d.IsAnonymous ? "Anonymous" : d.DonorName,
                Amount = d.Amount,
                CampaignTitle = d.Campaign!.Title,
                PaidAtUtc = d.PaidAtUtc ?? d.CreatedAtUtc
            })
            .ToListAsync(ct);

        var vm = new DonationTrackerViewModel
        {
            TotalRaised = totals.Sum(t => t.Raised),
            TotalDonations = totals.Sum(t => t.Count),
            ActiveCampaigns = campaigns.Count(c => c.IsActive),
            Campaigns = campaigns.Select(c =>
            {
                var t = totals.FirstOrDefault(x => x.CampaignId == c.Id);
                return new CampaignProgress { Campaign = c, Raised = t?.Raised ?? 0, DonorCount = t?.Count ?? 0 };
            }).ToList(),
            Recent = recent,
            ShowTestNotice = _opt.ShowTestModeNotice
        };

        return View(vm);
    }

    // =====================================================================
    //  PUBLIC: donation form  →  SSLCommerz
    // =====================================================================

    // GET /Donations/Donate/5
    public async Task<IActionResult> Donate(int id, CancellationToken ct)
    {
        var campaign = await db.DonationCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
        if (campaign is null) return NotFound();

        ViewBag.ShowTestNotice = _opt.ShowTestModeNotice;
        return View(new DonateViewModel
        {
            CampaignId = campaign.Id,
            CampaignTitle = campaign.Title,
            City = campaign.District ?? "Dhaka"
        });
    }

    // POST /Donations/Donate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Donate(DonateViewModel vm, CancellationToken ct)
    {
        // Never trust CampaignTitle / active-state from the form — reload from the database.
        var campaign = await db.DonationCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == vm.CampaignId && c.IsActive, ct);
        if (campaign is null) return NotFound();

        vm.CampaignTitle = campaign.Title;
        ViewBag.ShowTestNotice = _opt.ShowTestModeNotice;

        if (!Districts.List.Contains(vm.City))
            ModelState.AddModelError(nameof(vm.City), "Please choose a district from the list.");

        if (!ModelState.IsValid) return View(vm);

        // 1) Save the attempt FIRST (Pending), so every gateway callback has a row to match.
        var donation = new Donation
        {
            CampaignId = campaign.Id,
            DonorName = vm.DonorName.Trim(),
            DonorEmail = vm.DonorEmail.Trim(),
            DonorPhone = vm.DonorPhone.Trim(),
            IsAnonymous = vm.IsAnonymous,
            Amount = decimal.Round(vm.Amount, 2),
            Currency = "BDT",
            TranId = NewTranId(),
            Status = DonationStatus.Pending
        };
        db.Donations.Add(donation);
        await db.SaveChangesAsync(ct);

        // 2) Ask SSLCommerz for a payment page.
        var init = await ssl.InitiateAsync(donation, campaign.Title, vm.City,
            successUrl: CallbackUrl(nameof(Success)),
            failUrl: CallbackUrl(nameof(Fail)),
            cancelUrl: CallbackUrl(nameof(Cancel)),
            ipnUrl: CallbackUrl(nameof(Ipn)),
            ct);

        if (!init.Success || !IsSslCommerzUrl(init.GatewayUrl))
        {
            donation.Status = DonationStatus.Failed;
            donation.FailureReason = Truncate(init.Error ?? "Gateway returned an unexpected URL.", 255);
            await db.SaveChangesAsync(ct);

            ModelState.AddModelError(string.Empty, $"Payment could not be started: {init.Error}");
            return View(vm);
        }

        // 3) Send the donor to SSLCommerz's hosted payment page.
        return Redirect(init.GatewayUrl!);
    }

    // =====================================================================
    //  SSLCommerz callbacks
    //  - The donor's BROWSER is sent back here by an auto-submitted form POST from
    //    sslcommerz.com, so these actions cannot require an antiforgery token or login.
    //  - [AcceptVerbs] tolerates both GET and POST, reading values from form or query.
    // =====================================================================

    [AcceptVerbs("GET", "POST")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Success(CancellationToken ct)
    {
        var donation = await ConfirmAsync(Param("tran_id"), Param("val_id"), ct);

        if (donation is null)
            return View("PaymentResult", PaymentResult("Unknown transaction",
                "We could not find this donation. If money was deducted, contact the MHARS admin with your transaction ID.",
                "danger"));

        if (donation.Status == DonationStatus.Paid)
            return RedirectToAction(nameof(Receipt), new { id = donation.ReceiptToken });

        return View("PaymentResult", PaymentResult(
            donation.Status == DonationStatus.Pending ? "Payment pending confirmation" : "Payment could not be verified",
            donation.Status == DonationStatus.Pending
                ? "We could not reach the payment gateway to confirm your payment yet. It will be confirmed automatically when the gateway notifies us. Your transaction ID: " + donation.TranId
                : "The gateway did not confirm this payment. You have not been charged by MHARS. Transaction ID: " + donation.TranId,
            donation.Status == DonationStatus.Pending ? "warning" : "danger"));
    }

    [AcceptVerbs("GET", "POST")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Fail(CancellationToken ct)
    {
        await MarkUnpaidAsync(Param("tran_id"), DonationStatus.Failed, Param("error") ?? "Payment failed at gateway.", ct);
        return View("PaymentResult", PaymentResult("Payment failed",
            "Your payment did not go through, so nothing was charged. You can try again.", "danger"));
    }

    [AcceptVerbs("GET", "POST")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        await MarkUnpaidAsync(Param("tran_id"), DonationStatus.Cancelled, "Cancelled by donor.", ct);
        return View("PaymentResult", PaymentResult("Payment cancelled",
            "You cancelled the payment. Nothing was charged.", "secondary"));
    }

    /// <summary>
    /// IPN = Instant Payment Notification. SSLCommerz's SERVER calls this directly (no browser),
    /// so a donation is still confirmed even if the donor closes the tab before being redirected.
    /// Only works on the deployed site — sslcommerz.com cannot reach your localhost.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        var status = Param("status");
        var tranId = Param("tran_id");

        if (status == "VALID")
            await ConfirmAsync(tranId, Param("val_id"), ct);
        else if (status is "FAILED" or "EXPIRED" or "UNATTEMPTED")
            await MarkUnpaidAsync(tranId, DonationStatus.Failed, $"IPN status {status}", ct);
        else if (status == "CANCELLED")
            await MarkUnpaidAsync(tranId, DonationStatus.Cancelled, "IPN status CANCELLED", ct);

        return Ok(); // 200 tells SSLCommerz we received it
    }

    // =====================================================================
    //  PUBLIC: receipt (by unguessable token, not by int id)
    // =====================================================================

    // GET /Donations/Receipt/{guid}
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken ct)
    {
        var donation = await db.Donations.AsNoTracking()
            .Include(d => d.Campaign)
            .FirstOrDefaultAsync(d => d.ReceiptToken == id && d.Status == DonationStatus.Paid, ct);
        if (donation is null) return NotFound();

        ViewBag.ShowTestNotice = _opt.ShowTestModeNotice;
        return View(donation);
    }

    // =====================================================================
    //  ADMIN
    // =====================================================================

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        ViewBag.Campaigns = await db.DonationCampaigns.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CampaignProgress
            {
                Campaign = c,
                Raised = c.Donations.Where(d => d.Status == DonationStatus.Paid).Sum(d => (decimal?)d.Amount) ?? 0,
                DonorCount = c.Donations.Count(d => d.Status == DonationStatus.Paid)
            })
            .ToListAsync(ct);

        var transactions = await db.Donations.AsNoTracking()
            .Include(d => d.Campaign)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(200)
            .ToListAsync(ct);

        return View(transactions);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateCampaign() => View(new DonationCampaign { GoalAmount = 100000 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCampaign(
        [Bind("Title,Description,HazardType,District,GoalAmount,IsActive")] DonationCampaign campaign,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(campaign.District) && !Districts.List.Contains(campaign.District))
            ModelState.AddModelError(nameof(campaign.District), "Choose a district from the list, or leave it empty for nationwide.");

        if (!ModelState.IsValid) return View(campaign);

        campaign.CreatedAtUtc = DateTime.UtcNow;
        db.DonationCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);

        TempData["Success"] = $"Campaign \"{campaign.Title}\" created.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleCampaign(int id, CancellationToken ct)
    {
        var campaign = await db.DonationCampaigns.FindAsync([id], ct);
        if (campaign is null) return NotFound();

        campaign.IsActive = !campaign.IsActive;
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Manage));
    }

    // =====================================================================
    //  Helpers
    // =====================================================================

    /// <summary>
    /// The ONLY place a donation becomes Paid. Safe to call many times for the same
    /// transaction (success redirect + IPN may both arrive) — it is idempotent.
    /// </summary>
    private async Task<Donation?> ConfirmAsync(string? tranId, string? valId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tranId)) return null;

        var donation = await db.Donations.FirstOrDefaultAsync(d => d.TranId == tranId, ct);
        if (donation is null)
        {
            logger.LogWarning("Callback for unknown tran_id {TranId}", tranId);
            return null;
        }

        if (donation.Status == DonationStatus.Paid) return donation;   // already confirmed
        if (string.IsNullOrWhiteSpace(valId)) return donation;          // nothing to validate

        var v = await ssl.ValidateAsync(valId, ct);

        if (v.Status == "UNREACHABLE") return donation;                 // leave Pending; IPN can confirm later

        var amountMatches = v.Amount.HasValue && decimal.Round(v.Amount.Value, 2) == donation.Amount;
        var currencyMatches = string.Equals(v.Currency, donation.Currency, StringComparison.OrdinalIgnoreCase);
        var tranMatches = string.Equals(v.TranId, donation.TranId, StringComparison.Ordinal);

        if (v.IsValid && tranMatches && amountMatches && currencyMatches)
        {
            donation.Status = DonationStatus.Paid;
            donation.ValId = valId;
            donation.BankTranId = v.BankTranId;
            donation.CardType = v.CardType;
            donation.PaidAtUtc = DateTime.UtcNow;
            donation.FailureReason = null;
            logger.LogInformation("Donation {TranId} confirmed: {Amount} BDT", donation.TranId, donation.Amount);
        }
        else
        {
            donation.Status = DonationStatus.Failed;
            donation.FailureReason = Truncate(
                !v.IsValid ? v.Error ?? "Validation failed."
                : !tranMatches ? "tran_id mismatch between callback and validation."
                : !amountMatches ? $"Amount mismatch: expected {donation.Amount}, gateway says {v.Amount}."
                : "Currency mismatch.", 255);
            logger.LogWarning("Donation {TranId} NOT confirmed: {Reason}", donation.TranId, donation.FailureReason);
        }

        await db.SaveChangesAsync(ct);
        return donation;
    }

    /// <summary>Fail/Cancel never downgrade a Paid donation.</summary>
    private async Task MarkUnpaidAsync(string? tranId, DonationStatus newStatus, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tranId)) return;
        var donation = await db.Donations.FirstOrDefaultAsync(d => d.TranId == tranId, ct);
        if (donation is null || donation.Status != DonationStatus.Pending) return;

        donation.Status = newStatus;
        donation.FailureReason = Truncate(reason, 255);
        await db.SaveChangesAsync(ct);
    }

    private string? Param(string key)
    {
        if (Request.HasFormContentType && Request.Form.TryGetValue(key, out var f)) return f.ToString();
        return Request.Query.TryGetValue(key, out var q) ? q.ToString() : null;
    }

    private string CallbackUrl(string action)
    {
        if (!string.IsNullOrWhiteSpace(_opt.CallbackBaseUrl))
            return $"{_opt.CallbackBaseUrl.TrimEnd('/')}/Donations/{action}";

        return Url.Action(action, "Donations", null, Request.Scheme, Request.Host.Value)!;
    }

    /// <summary>MH + yyMMddHHmmss + 8 random hex = 22 chars (SSLCommerz limit is 30).</summary>
    private static string NewTranId() =>
        $"MH{DateTime.UtcNow:yyMMddHHmmss}{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";

    /// <summary>Only ever redirect donors to SSLCommerz, never to an arbitrary URL.</summary>
    private static bool IsSslCommerzUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && (u.Host == "sslcommerz.com" || u.Host.EndsWith(".sslcommerz.com", StringComparison.OrdinalIgnoreCase));

    private static PaymentResultViewModel PaymentResult(string title, string message, string tone) =>
        new() { Title = title, Message = message, Tone = tone };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

public class PaymentResultViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    /// <summary>Bootstrap colour: success / warning / danger / secondary.</summary>
    public string Tone { get; init; } = "secondary";
}
