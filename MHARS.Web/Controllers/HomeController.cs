using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

public class HomeController(ApplicationDbContext db, IUsgsEarthquakeService usgs) : Controller
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

        var earthquakes = await usgs.GetRecentEarthquakesAsync();
        var shelters = await db.Shelters.AsNoTracking()
            .OrderByDescending(s => s.Capacity)
            .Take(2)
            .ToListAsync();

        ViewBag.AllAlerts = allAlerts;
        ViewBag.Earthquakes = earthquakes.Take(3).ToList();
        ViewBag.Shelters = shelters;
        ViewBag.Districts = Districts.List;
        ViewBag.SelectedDistrict = district ?? "All";
        ViewBag.SelectedHazard = hazard;

        return View(filtered.Take(20).ToList());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
