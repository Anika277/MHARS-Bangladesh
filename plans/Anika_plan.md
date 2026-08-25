# Anika — External Data, Shelters & Analytics Plan

Branch: `anika/usgs-shelters-analytics`. Owns: `Shelter` model, `UsgsFeedService`, `ShelterController`, safety guidance content, `AnalyticsService`.

---

## Checkpoint 1: Shelter Model, Safety Content, USGS Stub

### Step 1.1 — `Shelter` Model (per design.md §2.3)
```csharp
public class Shelter
{
    public int ShelterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string ContactNumber { get; set; } = string.Empty;
    public string? ManagedByUserId { get; set; }
}
```
Register the `DbSet<Shelter>` with Farin — don't create a second `DbContext`, use the shared `AppDbContext`.

### Step 1.2 — Safety Guidance Content (feeds FR-6)
Write plain-language Do's/Don'ts as markdown or plain text you'll hand to Erin to drop into `Views/Safety/Flood.cshtml` and `Views/Safety/Earthquake.cshtml`. Keep it citable/factual (e.g. Drop-Cover-Hold for earthquakes, avoid walking through moving floodwater) — this content ships as static view text, not a database table (see design.md §2.3), so there's no model/controller work here, just the copy itself.

### Step 1.3 — `IUsgsFeedService` Interface (stub, don't finish yet)
```csharp
public interface IUsgsFeedService
{
    Task RefreshEarthquakeAlertsAsync();
}
```
Register a placeholder implementation in `Program.cs` so the DI container is wired before the real logic lands in CP2.

---

## Checkpoint 2: USGS Integration & Shelter CRUD

### Step 2.1 — `UsgsFeedService` (per design.md §3)
```csharp
public class UsgsFeedService : IUsgsFeedService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _context;

    public async Task RefreshEarthquakeAlertsAsync()
    {
        try
        {
            var geojson = await _http.GetFromJsonAsync<UsgsFeatureCollection>(
                "https://earthquake.usgs.gov/earthquakehazards/feed/v1.0/summary/significant_month.geojson");

            foreach (var feature in geojson.Features.Where(IsInRegion))
            {
                // upsert into Alerts with Source = Usgs, Severity from magnitude
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "USGS fetch failed — falling back to cached alerts");
            // swallow — NFR-2: app must keep working without this
        }
    }

    private bool IsInRegion(UsgsFeature f) =>
        f.Geometry.Coordinates[1] is >= 20 and <= 27 &&   // latitude band
        f.Geometry.Coordinates[0] is >= 88 and <= 93;      // longitude band
}
```
- Run this on a schedule — an `IHostedService` with `PeriodicTimer` (e.g. every 15–30 min) is the simplest correct approach; a scheduled call on app startup plus that timer covers both "always has data" and "doesn't hammer USGS."
- Severity mapping: `<4.0` → Low, `4.0–6.0` → Medium, `>6.0` → High (design.md §3.3).
- **Never let a USGS failure throw past this method** — catch and log, keep the last successfully fetched alerts in the DB as-is.

### Step 2.2 — `ShelterController`
- `[Authorize(Roles="Admin")]` CRUD actions (Create/Edit/Delete).
- `[AllowAnonymous]` `ByDistrict(string district)` for citizen search — mirror Farin's `Alert.ByDistrict` pattern so Erin's views stay consistent.
- Use the shared `Constants/Districts.cs` list from Farin's plan for the district dropdown, not free text.

---

## Final Checkpoint: Analytics

### Step 3.1 — `AnalyticsService` (feeds FR-8, data side of Erin's Chart.js view)
```csharp
public class AnalyticsService : IAnalyticsService
{
    public async Task<List<DistrictMonthCount>> GetAlertCountsAsync() =>
        await _context.Alerts
            .GroupBy(a => new { a.District, Month = a.IssuedAt.Month })
            .Select(g => new DistrictMonthCount
            {
                District = g.Key.District,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .ToListAsync();
}
```
Return this as JSON from an `AdminController` action (e.g. `GET /Admin/AnalyticsData`) so Erin's Chart.js can fetch it client-side — agree the exact JSON shape with her before you build the endpoint, not after.

### Step 3.2 — Hardening
- [ ] Confirm the app runs and shows flood alerts/shelters with wifi disabled (USGS failure path, NFR-2).
- [ ] Verify magnitude→severity boundaries with a couple of real USGS test values.
- [ ] Double-check district spellings in seeded `Shelter` rows match Farin's `Districts.cs` list exactly (case-sensitive text match, per design.md §2.2 trade-off).
