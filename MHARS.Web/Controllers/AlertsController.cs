using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;
using System.Security.Claims;
using System.Text;

namespace MHARS.Web.Controllers;

public class AlertsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? district, HazardType? hazard)
{
    var query = db.Alerts.AsNoTracking().OrderByDescending(a => a.IssuedAt).AsQueryable();

    if (!string.IsNullOrEmpty(district) && district != "All")
        query = query.Where(a => a.District == district);

    if (hazard.HasValue)
        query = query.Where(a => a.HazardType == hazard.Value);

    ViewBag.Districts = Districts.List;
    ViewBag.SelectedDistrict = district ?? "All";
    ViewBag.SelectedHazard = hazard;

    return View(await query.ToListAsync());
}

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportCsv()
    {
        var alerts = await db.Alerts.AsNoTracking()
            .OrderByDescending(a => a.IssuedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,HazardType,District,Title,Severity,IssuedAt,IssuedBy,SourceReference");
        foreach (var a in alerts)
        {
            sb.AppendLine(string.Join(",",
                a.Id,
                a.HazardType,
                Escape(a.District),
                Escape(a.Title),
                a.Severity,
                a.IssuedAt.ToString("yyyy-MM-dd HH:mm"),
                Escape(a.IssuedBy),
                Escape(a.SourceReference)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"alerts-export-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert == null) return NotFound();
        return View(alert);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        PopulateDropdowns();
        ViewBag.HazardList = new SelectList(
            new[] { new { Value = HazardType.Flood, Text = "Flood" } },
            "Value", "Text");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("HazardType,District,Title,Message,Severity,SourceReference")] Alert alert)
    {
        if (!ModelState.IsValid)
        {
            PopulateDropdowns();
            ViewBag.HazardList = new SelectList(
                new[] { new { Value = HazardType.Flood, Text = "Flood" } },
                "Value", "Text");
            return View(alert);
        }
        alert.IssuedAt = DateTime.UtcNow;
        alert.IssuedBy = User.FindFirstValue(ClaimTypes.Email);
        db.Add(alert);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var alert = await db.Alerts.FindAsync(id);
        if (alert == null) return NotFound();
        PopulateDropdowns();
        return View(alert);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,HazardType,District,Title,Message,Severity,SourceReference")] Alert alert)
    {
        if (id != alert.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            PopulateDropdowns();
            return View(alert);
        }
        try
        {
            var existing = await db.Alerts.FindAsync(id);
            if (existing == null) return NotFound();
            existing.HazardType = alert.HazardType;
            existing.District = alert.District;
            existing.Title = alert.Title;
            existing.Message = alert.Message;
            existing.Severity = alert.Severity;
            existing.SourceReference = alert.SourceReference;
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Alerts.AnyAsync(a => a.Id == id)) return NotFound();
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert == null) return NotFound();
        return View(alert);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var alert = await db.Alerts.FindAsync(id);
        if (alert != null)
        {
            db.Alerts.Remove(alert);
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private void PopulateDropdowns()
    {
        ViewBag.DistrictList = new SelectList(Districts.List);
        ViewBag.HazardList = new SelectList(
            Enum.GetValues<HazardType>().Select(h => new { Value = h, Text = h.ToString() }),
            "Value", "Text");
        ViewBag.SeverityList = new SelectList(
            Enum.GetValues<SeverityLevel>().Select(s => new { Value = s, Text = s.ToString() }),
            "Value", "Text");
    }
}