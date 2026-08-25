using Microsoft.AspNetCore.Mvc;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

public class EarthquakesController(IUsgsEarthquakeService usgs) : Controller
{
    public async Task<IActionResult> Index()
    {
        var quakes = await usgs.GetRecentEarthquakesAsync();
        return View(quakes);
    }
}
