using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? district, HazardType? hazard)
    {
        var allAlerts = await db.Alerts.AsNoTracking()
            .OrderByDescending(a => a.IssuedAt)
            .Take(30)
            .ToListAsync();

        var filtered = allAlerts.AsEnumerable();

        if (!string.IsNullOrEmpty(district) && district != "All")
            filtered = filtered.Where(a => a.District == district);

        if (hazard.HasValue)
            filtered = filtered.Where(a => a.HazardType == hazard.Value);

        // Reads our own table now instead of calling USGS on every page load.
        // Three reasons: the home page must render when USGS is unreachable, a visitor
        // must never wait on a third-party network call, and one visitor must not cost
        // one external request. The background sync service keeps this table fresh.
        //
        // Scope <= Regional means "in Bangladesh, or within the 700 km felt radius" —
        // a Myanmar quake that shakes Chattogram belongs on the national home page.
        // Magnitude != null reproduces the old service's filter: USGS occasionally
        // publishes an event before a magnitude is assigned, and the card has nothing
        // to show for those.
        var earthquakes = await db.EarthquakeEvents.AsNoTracking()
            .Where(e => e.Scope <= RegionScope.Regional && e.Magnitude != null)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(3)
            .ToListAsync();

        var shelters = await db.Shelters.AsNoTracking()
            .OrderByDescending(s => s.Capacity)
            .Take(2)
            .ToListAsync();

        ViewBag.AllAlerts = allAlerts;
        ViewBag.Earthquakes = earthquakes;
        ViewBag.Shelters = shelters;
        ViewBag.Districts = Districts.List;
        ViewBag.SelectedDistrict = district ?? "All";
        ViewBag.SelectedHazard = hazard;

        return View(filtered.Take(20).ToList());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}