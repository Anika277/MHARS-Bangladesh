# Farin — Identity, Alerts & Data Layer Plan

Branch: `farin/identity-alerts`. Owns: `AppDbContext`, `AppUser`, `Alert`, Identity roles, seed data, `AlertController`.

---

## Checkpoint 1: Foundation

### Step 1.1 — Solution Scaffolding
```
dotnet new mvc -o src/MHARS.Web -n MHARS.Web
dotnet new sln -n MHARS -o src
dotnet sln src/MHARS.sln add src/MHARS.Web/MHARS.Web.csproj
```
Add packages to `MHARS.Web.csproj`:
```
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

### Step 1.2 — `AppUser` (extends Identity)
```csharp
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 1.3 — `AppDbContext`
```csharp
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<Shelter> Shelters { get; set; } // Anika owns the model, you own the DbSet registration
}
```
Wire into `Program.cs`:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false; // relax for a course demo, note this in the report
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
```

### Step 1.4 — `Alert` Model (per design.md §2.2)
```csharp
public enum HazardType { Flood = 0, Earthquake = 1 }
public enum SeverityLevel { Low = 0, Medium = 1, High = 2 }
public enum AlertSource { Manual = 0, Usgs = 1 }

public class Alert
{
    public int AlertId { get; set; }
    public HazardType HazardType { get; set; }
    public string District { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; }
    public string? Description { get; set; }
    public AlertSource Source { get; set; }
    public decimal? Magnitude { get; set; }
    public decimal? Depth { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? IssuedByUserId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

### Step 1.5 — Roles & Seed Data (`Data/SeedData.cs`)
- Create `Admin` and `Citizen` roles on startup if missing.
- Seed one Admin user (document the demo credentials in the README, not hardcoded silently — graders may need to log in).
- Seed 2–3 sample flood `Alert` rows so Erin and Anika aren't blocked waiting on real data.

### Step 1.6 — First Migration
```
dotnet ef migrations add InitialCreate --project src/MHARS.Web
dotnet ef database update --project src/MHARS.Web
```

### Step 1.7 — Shared District List (`Constants/Districts.cs`) — **do this in CP1, not CP2**
Put the canonical Bangladesh district list in a static `List<string>` in `Constants/Districts.cs`. This is a **blocking dependency**: Erin's dropdowns and Anika's shelter seed data must match it exactly (case-sensitive text match, per design.md §2.2). Commit it early so nobody hand-types their own version.

---

## Checkpoint 2: Alert CRUD & District Filtering

### Step 2.1 — `AlertController` (Admin side)
- `[Authorize(Roles = "Admin")]` on Create/Edit/Deactivate actions.
- `Create(AlertCreateViewModel)` — flood alerts only; `HazardType` locked to `Flood` in the form since earthquake rows come from USGS (Anika's service writes those directly).
- `Edit` / `Deactivate` (soft delete via `IsActive = false`, not a hard delete — keeps history for the analytics track).

### Step 2.2 — District Filter (Citizen side, feeds FR-4)
```csharp
[AllowAnonymous]
public async Task<IActionResult> ByDistrict(string district)
{
    var alerts = await _context.Alerts
        .Where(a => a.IsActive && a.District == district)
        .OrderByDescending(a => a.IssuedAt)
        .ToListAsync();
    return View(alerts); // Erin's view consumes this
}
```
Coordinate with Erin on the exact `district` values (should match a fixed list of Bangladesh districts, not free-typed by the citizen — a dropdown, not a text box, to avoid typo mismatches against `Shelter.District`).

### Step 2.3 — Share the District List
Already created in Step 1.7 — just make sure `Alert.District` values you write/seed come from that list, never free text.

---

## Final Checkpoint: Hardening

- [ ] Confirm `[Authorize(Roles="Admin")]` actually blocks a logged-out or Citizen-role request (test both).
- [ ] Add basic model validation (`[Required]`, `[StringLength]`) on `Alert` create/edit forms.
- [ ] Verify soft-deleted (`IsActive = false`) alerts don't appear in citizen views but do appear in Admin's analytics history for the dashboard.
- [ ] Walk through the demo script with Erin and Anika — you're likely narrating the Admin alert-issuing flow live.

---

## Team Dependencies (who waits on whom)

| You need... | From | By when |
|---|---|---|
| Confirmation of `Shelter` model shape before first migration | Anika (Step 1.1) | Before `InitialCreate` — otherwise a second migration is needed immediately |
| ViewModel field names for Alert Create/Edit forms | Erin | Start of Checkpoint 2 — her forms bind to what your controller passes |

| They need... | What | When |
|---|---|---|
| Erin needs | `Constants/Districts.cs` list | **End of Checkpoint 1** — blocks all her dropdowns |
| Erin needs | `AlertController.ByDistrict` action + seeded sample alerts | End of Checkpoint 1 / early CP2 |
| Anika needs | `AppDbContext` with `DbSet<Shelter>` registered, Identity wired in `Program.cs` | End of Checkpoint 1 |
| Both need | Seed data (Admin login + 2–3 flood alerts) so they aren't blocked on real data | End of Checkpoint 1 |

**Key rule:** you are the critical path of Checkpoint 1 — everyone else is blocked until your migration lands and is pushed. Prioritize Steps 1.1–1.6 over everything else.
