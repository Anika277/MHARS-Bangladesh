using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

public class RiskIndexController(ApplicationDbContext db) : Controller
{
    private static readonly string[] Categories = { "Very Low", "Low", "Medium", "High", "Very High" };
    private static readonly string[] ValidHazards = { "Overall", "Flood", "Earthquake" };

    /// <summary>Days of seismic history that feed the exposure sum.</summary>
    private const int SeismicWindowDays = 365;

    /// <summary>
    /// Events beyond this epicentral distance are dropped entirely.
    ///
    /// This is the fix for every district scoring identically. Bangladesh is about
    /// 600 km across, so an event 5000 km away sits 5000 km from Khulna and 4940 km
    /// from Sylhet — near enough the same for both. Summing far-field events adds a
    /// large constant to every district and drowns the near-field differences that
    /// actually distinguish them. Only nearby seismicity carries spatial information.
    /// </summary>
    private const double ExposureCutoffKm = 1200.0;

    public async Task<IActionResult> Index(string hazard = "Overall")
    {
        if (!ValidHazards.Contains(hazard)) hazard = "Overall";

        var alerts = await db.Alerts.AsNoTracking().ToListAsync();

        // Earthquake risk comes from the EarthquakeEvents table — real located events —
        // not from the seeded earthquake Alert. An alert is a human notification;
        // scoring hazard from notifications measures how busy the admin was, not where
        // the faults are.
        //
        // Scope <= Regional keeps this to the source zones that generate Bangladesh's
        // hazard. A Japanese megathrust event is real seismicity and irrelevant here.
        var since = DateTime.UtcNow.AddDays(-SeismicWindowDays);
        var quakes = await db.EarthquakeEvents.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= since
                     && e.Magnitude != null
                     && e.Scope <= RegionScope.Regional)
            .Select(e => new QuakePoint(e.Latitude, e.Longitude, e.DepthKm, e.Magnitude!.Value))
            .ToListAsync();

        // Only districts with coordinates can be scored or mapped. Scoring a district
        // at (0,0) would place it in the Atlantic and produce a confident, meaningless
        // number.
        var located = Districts.List
            .Where(d => Districts.Coordinates.ContainsKey(d))
            .ToList();

        var raw = located.Select(d =>
        {
            var coords = Districts.Coordinates[d];

            var floodRaw = (double)alerts
                .Where(a => a.District == d && a.HazardType == HazardType.Flood)
                .Sum(a => SeverityWeight(a.Severity));

            var quakeRaw = SeismicExposure(coords.Lat, coords.Lng, quakes);

            return (District: d, coords.Lat, coords.Lng, FloodRaw: floodRaw, QuakeRaw: quakeRaw);
        }).ToList();

        // Normalise against the highest-scoring district rather than a hand-picked
        // constant. The previous version multiplied by a fixed 12 and clamped at 100,
        // which saturated every district the moment the raw numbers grew. A relative
        // index needs a relative scale, and this one recalibrates itself as data
        // arrives instead of depending on a number guessed up front.
        double maxFlood = raw.Count == 0 ? 0 : raw.Max(x => x.FloodRaw);
        double maxQuake = raw.Count == 0 ? 0 : raw.Max(x => x.QuakeRaw);

        var rows = raw.Select(x =>
        {
            int flood = Normalise(x.FloodRaw, maxFlood);
            int quake = Normalise(x.QuakeRaw, maxQuake);
            int overall = (int)Math.Round((flood + quake) / 2.0);

            return new DistrictRisk
            {
                District = x.District,
                Lat = x.Lat,
                Lng = x.Lng,
                FloodScore = flood,
                FloodCategory = Band(flood),
                EarthquakeScore = quake,
                EarthquakeCategory = Band(quake),
                OverallScore = overall,
                OverallCategory = Band(overall)
            };
        }).ToList();

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

        var top = rows.OrderByDescending(r => r.EarthquakeScore).FirstOrDefault();

        ViewBag.SelectedHazard = hazard;
        ViewBag.MapData = System.Text.Json.JsonSerializer.Serialize(mapData);
        ViewBag.CategoryLabelsWithPercent = System.Text.Json.JsonSerializer.Serialize(labelsWithPercent);
        ViewBag.CategoryCounts = System.Text.Json.JsonSerializer.Serialize(categoryCounts);
        ViewBag.CoverageNote = BuildCoverageNote(rows.Count, quakes.Count, top);

        return View(rows.OrderBy(r => r.District).ToList());
    }

    private static string BuildCoverageNote(int scored, int quakeCount, DistrictRisk? top)
    {
        var note = $"Scored {scored} of {Districts.List.Count()} districts; the rest have no coordinates on file yet. "
                 + $"Earthquake exposure uses {quakeCount} located events from the regional source zone over the last {SeismicWindowDays} days, "
                 + $"within {ExposureCutoffKm:0} km. Scores are relative: 100 is the most exposed district, not an absolute hazard level.";

        if (quakeCount == 0)
            note += " No regional events are stored yet, so every earthquake score is zero — run a sync first.";
        else if (top is not null)
            note += $" Most exposed: {top.District}.";

        return note;
    }

    private readonly record struct QuakePoint(double Lat, double Lon, double DepthKm, double Magnitude);

    /// <summary>
    /// Relative seismic exposure at a point: the summed shaking contribution of nearby
    /// recorded events, weighted by magnitude and attenuated by hypocentral distance.
    ///
    ///     contribution = 10^(M - 4) / (1 + (R/100)^2)
    ///
    /// The numerator follows the Richter scale, which is logarithmic in amplitude, so a
    /// M6 contributes a hundred times a M4 at the same distance. The denominator falls
    /// off as distance squared, so a nearby moderate event outweighs a distant large
    /// one. R is hypocentral, not epicentral — a 100 km deep event directly underfoot is
    /// 100 km away, not zero.
    ///
    /// This is a deliberately simple heuristic, NOT a ground motion prediction equation.
    /// It supports "this district has seen more nearby shaking than that one". It does
    /// NOT support any claim about probability, return period, or expected peak ground
    /// acceleration — a real PSHA needs fault geometry, recurrence intervals and site
    /// soil classes, none of which this project has.
    /// </summary>
    private static double SeismicExposure(double lat, double lon, IReadOnlyList<QuakePoint> quakes)
    {
        double total = 0;

        foreach (var q in quakes)
        {
            var epi = SeismicGeography.HaversineKm(lat, lon, q.Lat, q.Lon);
            if (epi > ExposureCutoffKm) continue;

            var r = Math.Sqrt(epi * epi + q.DepthKm * q.DepthKm);
            total += Math.Pow(10, q.Magnitude - 4.0) / (1.0 + Math.Pow(r / 100.0, 2));
        }

        return total;
    }

    private static int Normalise(double value, double max)
        => max <= 0 ? 0 : (int)Math.Round(Math.Clamp(value / max * 100.0, 0, 100));

    /// <summary>
    /// Absolute bands on the 0-100 relative index.
    ///
    /// The original version ranked districts into quintiles, which forced exactly 20%
    /// into each category whatever the data said — the pie could never change, and a
    /// district with no recorded hazard still came out "High" because something had to
    /// fill that fifth. Fixed thresholds mean an empty category is a real result.
    /// </summary>
    private static string Band(int index) => index switch
    {
        <= 0 => "Very Low",
        <= 20 => "Low",
        <= 40 => "Medium",
        <= 70 => "High",
        _ => "Very High"
    };

    private static int SeverityWeight(SeverityLevel s) => s switch
    {
        SeverityLevel.High => 3,
        SeverityLevel.Medium => 2,
        _ => 1
    };
}