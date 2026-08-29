using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

public class RiskIndexController(ApplicationDbContext db) : Controller
{
    private static readonly string[] Categories = { "Very Low", "Low", "Medium", "High", "Very High" };
    private static readonly string[] ValidHazards = { "Overall", "Flood", "Earthquake" };

    public async Task<IActionResult> Index(string hazard = "Overall")
    {
        if (!ValidHazards.Contains(hazard)) hazard = "Overall";

        var alerts = await db.Alerts.AsNoTracking().ToListAsync();

        var rows = Districts.List.Select(d =>
        {
            var floodScore = alerts.Where(a => a.District == d && a.HazardType == HazardType.Flood)
                                    .Sum(a => SeverityWeight(a.Severity));
            var quakeScore = alerts.Where(a => a.District == d && a.HazardType == HazardType.Earthquake)
                                    .Sum(a => SeverityWeight(a.Severity));
            Districts.Coordinates.TryGetValue(d, out var coords);

            return new DistrictRisk
            {
                District = d,
                Lat = coords.Lat,
                Lng = coords.Lng,
                FloodScore = floodScore,
                EarthquakeScore = quakeScore,
                OverallScore = floodScore + quakeScore
            };
        }).ToList();

        AssignCategories(rows, r => r.FloodScore, (r, c) => r.FloodCategory = c);
        AssignCategories(rows, r => r.EarthquakeScore, (r, c) => r.EarthquakeCategory = c);
        AssignCategories(rows, r => r.OverallScore, (r, c) => r.OverallCategory = c);

        Func<DistrictRisk, string> selectedCategory = hazard switch
        {
            "Flood" => r => r.FloodCategory,
            "Earthquake" => r => r.EarthquakeCategory,
            _ => r => r.OverallCategory
        };
        Func<DistrictRisk, int> selectedScore = hazard switch
        {
            "Flood" => r => r.FloodScore,
            "Earthquake" => r => r.EarthquakeScore,
            _ => r => r.OverallScore
        };

        var mapData = rows.Select(r => new
        {
            r.District,
            r.Lat,
            r.Lng,
            Category = selectedCategory(r),
            Score = selectedScore(r)
        }).ToList();

        int total = rows.Count;
        var categoryCounts = Categories
            .Select(c => rows.Count(r => selectedCategory(r) == c))
            .ToList();
        var categoryPercents = categoryCounts
            .Select(c => total == 0 ? 0 : Math.Round(c * 100.0 / total, 1))
            .ToList();
        var labelsWithPercent = Categories
            .Zip(categoryPercents, (cat, pct) => $"{cat} ({pct}%)")
            .ToList();

        ViewBag.SelectedHazard = hazard;
        ViewBag.MapData = System.Text.Json.JsonSerializer.Serialize(mapData);
        ViewBag.CategoryLabelsWithPercent = System.Text.Json.JsonSerializer.Serialize(labelsWithPercent);
        ViewBag.CategoryCounts = System.Text.Json.JsonSerializer.Serialize(categoryCounts);

        return View(rows.OrderBy(r => r.District).ToList());
    }

    private static void AssignCategories(List<DistrictRisk> rows, Func<DistrictRisk, int> scoreSelector, Action<DistrictRisk, string> setCategory)
    {
        var ranked = rows.OrderBy(scoreSelector).ToList();
        int n = ranked.Count;
        for (int i = 0; i < n; i++)
        {
            int bucket = n <= 1 ? 0 : Math.Min(4, i * Categories.Length / n);
            setCategory(ranked[i], Categories[bucket]);
        }
    }

    private static int SeverityWeight(SeverityLevel s) => s switch
    {
        SeverityLevel.High => 3,
        SeverityLevel.Medium => 2,
        _ => 1
    };
}