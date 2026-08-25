using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

public class GuidelinesController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(HazardType? hazard)
    {
        var guidelines = await db.SafetyGuidelines
            .AsNoTracking()
            .Where(g => !hazard.HasValue || g.HazardType == hazard.Value)
            .OrderBy(g => g.HazardType).ThenBy(g => g.SortOrder)
            .ToListAsync();
        return View(guidelines);
    }
}
