using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var totalAlerts = await db.Alerts.CountAsync();
        var floodCount = await db.Alerts.CountAsync(a => a.HazardType == HazardType.Flood);
        var quakeCount = await db.Alerts.CountAsync(a => a.HazardType == HazardType.Earthquake);
        var shelters = await db.Shelters.CountAsync();

                var perDistrict = await db.Alerts
            .GroupBy(a => a.District)
            .Select(g => new { District = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var perMonth = await db.Alerts
            .GroupBy(a => new { a.IssuedAt.Year, a.IssuedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new { Label = g.Key.Year + "-" + g.Key.Month.ToString("00"), Count = g.Count() })
            .ToListAsync();

        var bySeverity = await db.Alerts
            .GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        ViewBag.TotalAlerts = totalAlerts;
        ViewBag.FloodCount = floodCount;
        ViewBag.QuakeCount = quakeCount;
        ViewBag.ShelterCount = shelters;
        ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(perDistrict.Select(x => x.District));
        ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(perDistrict.Select(x => x.Count));
        ViewBag.MonthLabels = System.Text.Json.JsonSerializer.Serialize(perMonth.Select(x => x.Label));
        ViewBag.MonthData = System.Text.Json.JsonSerializer.Serialize(perMonth.Select(x => x.Count));
        ViewBag.SeverityLabels = System.Text.Json.JsonSerializer.Serialize(bySeverity.Select(x => x.Severity));
        ViewBag.SeverityData = System.Text.Json.JsonSerializer.Serialize(bySeverity.Select(x => x.Count));

        return View(perDistrict);
    }
}
