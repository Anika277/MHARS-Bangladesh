# Anika — External Data, Shelters & Analytics Plan

Branch: `anika/usgs-shelters-analytics`. Owns: `Shelter` model, `UsgsFeedService`, `ShelterController`, safety guidance content. *(Analytics data moved to Erin's plan — see her Final Checkpoint; you focus on USGS integration, which is this project's hardest single component.)*

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
                "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/significant_month.geojson");

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
> [!NOTE]
> USGS GeoJSON returns coordinates as JSON numbers (`double`). Define your DTO classes with `double` properties and convert to `decimal` when mapping into the `Alert` entity — `GetFromJsonAsync` will throw or mis-map if you declare them as `decimal` directly.
- Run this on a schedule — an `IHostedService` with `PeriodicTimer` (e.g. every 15–30 min) is the simplest correct approach; a scheduled call on app startup plus that timer covers both "always has data" and "doesn't hammer USGS." If the `IHostedService` wiring slows you down, pair with Farin (she owns `Program.cs` DI config) — split it: you write the service class, she registers it.
- Severity mapping: `<4.0` → Low, `4.0–6.0` → Medium, `>6.0` → High (design.md §3.3).
- **Never let a USGS failure throw past this method** — catch and log, keep the last successfully fetched alerts in the DB as-is.

### Step 2.2 — `ShelterController`
- `[Authorize(Roles="Admin")]` CRUD actions (Create/Edit/Delete).
- `[AllowAnonymous]` `ByDistrict(string district)` for citizen search — mirror Farin's `Alert.ByDistrict` pattern so Erin's views stay consistent.
- Use the shared `Constants/Districts.cs` list from Farin's plan for the district dropdown, not free text.

---

## Final Checkpoint: Hardening & Handoff

> [!NOTE]
> The analytics service and `/Admin/AnalyticsData` endpoint were **moved to Erin's plan** to balance workload. Your Final Checkpoint is now hardening only.

### Step 3.1 — Hardening
- [ ] Confirm the app runs and shows flood alerts/shelters with wifi disabled (USGS failure path, NFR-2).
- [ ] Verify magnitude→severity boundaries with a couple of real USGS test values.
- [ ] Double-check district spellings in seeded `Shelter` rows match Farin's `Districts.cs` list exactly (case-sensitive text match, per design.md §2.2 trade-off).
- [ ] Hand Erin a few sample `Alert` rows (via Farin's seed data or a quick manual insert) so she can test the analytics chart against real-shaped data.

---

## Team Dependencies (who waits on whom)

| You need... | From | By when |
|---|---|---|
| `AppDbContext` created + Identity wired in `Program.cs` | Farin (Steps 1.2–1.3, 1.7) | End of Checkpoint 1 — you can't migrate `Shelter` until her DbContext exists |
| `Constants/Districts.cs` shared list | Farin (Step 1.7) | End of Checkpoint 1 — shelter seed rows must match exactly |
| DI registration help for `IHostedService` | Farin (she owns `Program.cs`) | When wiring the USGS timer in CP2 |

| They need... | What | When |
|---|---|---|
| Erin needs | Safety guidance copy (Step 1.2) | End of Checkpoint 1 |
| Erin needs | Shelter ViewModel shape for Create/Edit/ByDistrict views | Start of Checkpoint 2 |
| Farin needs | Confirmation of the final `Shelter` model before his first migration | **Start of Checkpoint 1 — send it to him Day 1** |

**Key rule:** your `Shelter` model definition blocks Farin's first migration — write and share that class on Day 1 even if everything else isn't ready.
