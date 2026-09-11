using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;
using MHARS.Web.Models.ViewModels;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

public class EarthquakesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UsgsEarthquakeService _usgs;
    private readonly UsgsSyncStatus _status;

    private const int MaxRows = 300;

    public EarthquakesController(ApplicationDbContext db, UsgsEarthquakeService usgs, UsgsSyncStatus status)
    {
        _db = db;
        _usgs = usgs;
        _status = status;
    }

    public async Task<IActionResult> Index(string scope = "bangladesh", int days = 30, double? minMag = null)
    {
        var activeScope = ParseScope(scope);
        days = Math.Clamp(days, 1, 90);

        var since = DateTime.UtcNow.AddDays(-days);

        // Containment semantics: RegionScope is ordered most-specific-first, so
        // "Scope <= Regional" means "in Bangladesh OR in the felt zone around it".
        // A Sylhet quake therefore appears under Bangladesh, Regional and Asia —
        // which is correct, because it genuinely is all three.
        IQueryable<EarthquakeEvent> baseQuery = _db.EarthquakeEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= since);

        var scoped = baseQuery.Where(e => e.Scope <= activeScope);

        if (minMag is not null)
            scoped = scoped.Where(e => e.Magnitude >= minMag.Value);

        var events = await scoped
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(MaxRows)
            .ToListAsync();

        var dayAgo = DateTime.UtcNow.AddDays(-1);
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var tabs = new List<ScopeTab>
        {
            new(RegionScope.Bangladesh, "bangladesh", "বাংলাদেশ", "Bangladesh",
                "জাতীয় সীমানা ও সংলগ্ন এলাকা", await CountAsync(baseQuery, RegionScope.Bangladesh)),

            new(RegionScope.Regional, "regional", "আঞ্চলিক উৎস", "Source zone",
                "১৮–৩০°উ, ৮৫–৯৮°পূ — ইন্দো-বার্মান, ডাউকি ফল্ট, আসাম, হিমালয়ান থ্রাস্ট", await CountAsync(baseQuery, RegionScope.Regional)),

            new(RegionScope.Asia, "asia", "এশিয়া", "Asia",
                "এশিয়া মহাদেশ, M4.5+", await CountAsync(baseQuery, RegionScope.Asia))
        };

        var vm = new EarthquakeDashboardViewModel
        {
            ActiveScope = activeScope,
            Events = events,
            Tabs = tabs,
            LookbackDays = days,
            CountLast24Hours = await scoped.CountAsync(e => e.OccurredAtUtc >= dayAgo),
            CountLast7Days = await scoped.CountAsync(e => e.OccurredAtUtc >= weekAgo),
            Strongest = await scoped.OrderByDescending(e => e.Magnitude).FirstOrDefaultAsync(),
            MostRecent = events.FirstOrDefault(),
            LastSyncUtc = _status.LastSuccessUtc,
            SyncError = _status.LastError,
            MapPointsJson = BuildMapJson(events)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(string scope = "bangladesh", int days = 30)
    {
        var remaining = _status.ManualCooldownRemaining(_usgs.Options.ManualRefreshCooldownSeconds);

        if (remaining > 0)
        {
            TempData["SyncMessage"] = $"Already refreshed a moment ago. Try again in {remaining} second{(remaining == 1 ? "" : "s")}.";
            return RedirectToAction(nameof(Index), new { scope, days });
        }

        _status.RecordManualRefresh();
        var result = await _usgs.SyncAsync(HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["SyncMessage"] = $"Refresh failed: {result.Error}";
        }
        else if (result.Inserted == 0 && result.Updated == 0)
        {
            // Say what actually happened. "0 new events" reads like a failure; it isn't.
            TempData["SyncMessage"] = $"Checked USGS — {result.Fetched} events in the feed, nothing new since the last sync.";
        }
        else
        {
            TempData["SyncMessage"] = $"Refreshed: {result.Inserted} new, {result.Updated} revised, {result.Fetched} checked.";
        }

        return RedirectToAction(nameof(Index), new { scope, days });
    }

    /// <summary>
    /// Recomputes Scope, distance from Dhaka and nearest division for every stored row
    /// straight from its coordinates.
    ///
    /// Needed because those three values are OUR derivation, not USGS data. Any change
    /// to the bounding boxes or the felt radius leaves existing rows holding answers
    /// from the old rules, and a normal sync would not notice — the tabs would quietly
    /// show wrong counts. Run this after editing SeismicGeography.
    ///
    /// TODO before this is ever reachable in production: add [Authorize(Roles = "Admin")].
    /// It rewrites every row in the table.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reclassify(string scope = "bangladesh", int days = 30)
    {
        var rows = await _db.EarthquakeEvents.ToListAsync();
        int changed = 0;

        foreach (var row in rows)
        {
            var newScope = SeismicGeography.Classify(row.Latitude, row.Longitude);
            var newDhakaKm = Math.Round(SeismicGeography.DistanceFromDhakaKm(row.Latitude, row.Longitude), 1);
            var (divisionName, divisionKm) = SeismicGeography.NearestDivision(row.Latitude, row.Longitude);
            divisionKm = Math.Round(divisionKm, 1);

            if (row.Scope == newScope
                && row.DistanceFromDhakaKm == newDhakaKm
                && row.NearestDivision == divisionName
                && row.NearestDivisionKm == divisionKm)
            {
                continue;
            }

            row.Scope = newScope;
            row.DistanceFromDhakaKm = newDhakaKm;
            row.NearestDivision = divisionName;
            row.NearestDivisionKm = divisionKm;
            changed++;
        }

        await _db.SaveChangesAsync();

        var summary = rows
            .GroupBy(r => r.Scope)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key} {g.Count()}");

        TempData["SyncMessage"] = $"Reclassified {changed} of {rows.Count} rows. Now: {string.Join(", ", summary)}.";

        return RedirectToAction(nameof(Index), new { scope, days });
    }

    private static async Task<int> CountAsync(IQueryable<EarthquakeEvent> q, RegionScope scope)
        => await q.CountAsync(e => e.Scope <= scope);

    private static RegionScope ParseScope(string? value) => value?.ToLowerInvariant() switch
    {
        "regional" => RegionScope.Regional,
        "asia" => RegionScope.Asia,
        "world" or "global" => RegionScope.Global,
        _ => RegionScope.Bangladesh
    };

    private static string BuildMapJson(IReadOnlyList<EarthquakeEvent> events)
    {
        var points = events
            .Where(e => e.Magnitude.HasValue)
            .Take(200)
            .Select(e => new
            {
                lat = e.Latitude,
                lon = e.Longitude,
                mag = e.Magnitude,
                band = e.MagnitudeBand,
                place = e.Place,
                depth = e.DepthKm,
                time = BdTime.ToBst(e.OccurredAtUtc).ToString("dd MMM yyyy, HH:mm") + " BST",
                url = e.DetailsUrl
            });

        return JsonSerializer.Serialize(points);
    }
}