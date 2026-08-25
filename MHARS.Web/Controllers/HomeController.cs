using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
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

        var alerts = await query.Take(20).ToListAsync();
        return View(alerts);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
