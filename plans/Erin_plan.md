# Erin — Frontend Plan (Views, Bootstrap UI, Chart.js)

Branch: `erin/frontend-views`. Owns: `_Layout.cshtml`, every Razor View, `wwwroot`, Chart.js rendering, responsive design (FR-9). You don't own any `DbContext`/model changes — you consume ViewModels that Farin and Anika's controllers pass in.

---

## Setup: The Mock Data Approach

Don't wait on Farin's `AlertController` or Anika's `ShelterController` to start building views. Build against **ViewModels** (plain C# classes with no EF dependency) and hardcoded sample lists first, then swap the hardcoded list for the real controller data once it exists — the Razor view barely changes.

```csharp
// Models/ViewModels/AlertViewModel.cs — no EF, no DB dependency
public class AlertViewModel
{
    public string HazardType { get; set; } = "Flood";
    public string District { get; set; } = "Dhaka";
    public string Severity { get; set; } = "Medium";
    public string? Description { get; set; }
    public decimal? Magnitude { get; set; }
    public DateTime IssuedAt { get; set; }
}
```
```csharp
// Temporary mock data for building the view before Farin's controller exists
var sampleAlerts = new List<AlertViewModel>
{
    new() { HazardType = "Flood", District = "Sirajganj", Severity = "High", Description = "FFWC bulletin: river above danger level", IssuedAt = DateTime.Now.AddHours(-3) },
    new() { HazardType = "Earthquake", District = "Chittagong", Severity = "Medium", Magnitude = 4.8m, IssuedAt = DateTime.Now.AddHours(-1) },
};
```
Once Farin's real `AlertController.ByDistrict` action exists, you just change the controller's data source from the mock list to `_context.Alerts...` — the `View(alerts)` call and the `.cshtml` markup don't need to change if the ViewModel shape matches.

---

## Checkpoint 1: Foundation Views

### Step 1.1 — `_Layout.cshtml` (Master Layout)
- Navbar: MHARS logo/title, links to Home, Alerts, Shelters, Safety Guidance, and (Admin-only, conditionally rendered with `User.IsInRole("Admin")`) an Admin dropdown.
- Footer: course/team attribution.
- Bootstrap 5 via CDN or `wwwroot/lib` (LibMan is fine for a course project — don't hand-roll CSS you don't need to).

### Step 1.2 — `wwwroot` Setup
- `wwwroot/lib/bootstrap` (Bootstrap 5 CSS/JS)
- `wwwroot/lib/chart.js` (for the Final Checkpoint dashboard)
- `wwwroot/css/site.css` — a small custom stylesheet for hazard-severity color badges (Low = green, Medium = amber, High = red) since these appear on multiple pages.

### Step 1.3 — Home / `Index.cshtml`
- Hero section: "Is my district safe right now?" with a district dropdown that submits straight to the Alert-by-district view (this is the "reachable in 2 clicks" flow from NFR-1 — don't bury it under a menu).
- Three quick links below: Shelters, Safety Guidance, (Admin) Login.

### Step 1.4 — Login View
- Standard ASP.NET Identity login scaffolding, restyled with Bootstrap. Citizens never need to see a Register page if the team decides citizen accounts aren't needed — confirm with Farin whether Register should even be linked in the navbar, since most FRs are guest-accessible.

---

## Checkpoint 2: Core Flow Views

### Step 2.1 — District Alert View (FR-4) — `Views/Alert/ByDistrict.cshtml`
- District dropdown (reuse the shared district list — ask Farin for `Constants/Districts.cs` so your dropdown options match exactly what's stored in the DB, not a hand-typed list that drifts).
- Two sections on the results page: **Flood Alerts** and **Earthquake Alerts** (or a single list with a hazard-type badge — either works, pick whichever renders cleaner with your severity badges).
- Empty state: "No active alerts for [District] right now" — don't show a blank page.
- Earthquake-unavailable state: if the ViewModel/controller signals the USGS feed failed, show a small dismissible banner ("Live earthquake feed temporarily unavailable — showing flood alerts only") rather than hiding the section silently (NFR-2).

### Step 2.2 — Shelter Search View (FR-5) — `Views/Shelter/ByDistrict.cshtml`
- Same district-dropdown pattern as alerts, for consistency.
- Card or table layout: Name, Address, Capacity, Contact (tap-to-call `<a href="tel:...">` on mobile — this matters for an emergency-use app).

### Step 2.3 — Safety Guidance Views (FR-6) — `Views/Safety/Flood.cshtml`, `Views/Safety/Earthquake.cshtml`
- Static content pages using Anika's copy. Two-column Do's/Don'ts layout, icon or checkmark styling, no dynamic data — these are the simplest views in the app, good ones to build first while waiting on the others' controllers.

### Step 2.4 — Admin CRUD Forms
- `Views/Alert/Create.cshtml` / `Edit.cshtml` — bind to Farin's `AlertCreateViewModel`; hazard type locked to Flood (earthquake rows come from USGS, not this form — say so in a form hint so Farin/graders don't wonder why the dropdown is missing an option).
- `Views/Shelter/Create.cshtml` / `Edit.cshtml` — bind to Anika's shelter ViewModel.
- Keep both forms visually consistent (same label/input/validation-summary pattern) — copy one Razor form structure and adapt the fields rather than styling each from scratch.

---

## Final Checkpoint: Analytics Dashboard + Polish

### Step 3.1 — Chart.js Analytics View (FR-8) — `Views/Admin/Analytics.cshtml`
- Fetch Anika's JSON endpoint client-side (`fetch('/Admin/AnalyticsData')`) and render a bar or line chart of alert counts per district/month.
- Agree the JSON shape with Anika **before** building this (e.g. `[{ district, month, count }]`) — don't guess and reshape later.
```html
<canvas id="alertChart"></canvas>
<script>
  fetch('/Admin/AnalyticsData')
    .then(r => r.json())
    .then(data => {
      // group by district or month depending on what reads clearest for a course demo
      new Chart(document.getElementById('alertChart'), {
        type: 'bar',
        data: { /* labels + datasets built from `data` */ },
      });
    });
</script>
```

### Step 3.2 — Responsive Pass (FR-9)
- Test every page at mobile width (Bootstrap's `col-*` breakpoints, or just resize the browser) — this app's whole pitch is "check from your phone during an emergency," so a desktop-only layout undercuts the project's own problem statement.
- Confirm the district dropdown, alert cards, and shelter contact links are all comfortably tappable on a small screen.

### Step 3.3 — Demo Readiness
- [ ] Walk the full citizen path live: Home → pick district → see alerts → find a shelter → read safety guidance.
- [ ] Walk the full Admin path live: log in → issue a flood alert → see it appear in the citizen view → check the analytics chart updates.
- [ ] Confirm no page crashes with an empty district (no alerts/shelters yet) or with the USGS feed unreachable.

---

## API Contract Reference (Controller → View)

| Controller Action | Your View | Notes |
|---|---|---|
| `AlertController.ByDistrict(district)` | `Views/Alert/ByDistrict.cshtml` | Farin |
| `AlertController.Create/Edit` (Admin) | `Views/Alert/Create.cshtml`, `Edit.cshtml` | Farin |
| `ShelterController.ByDistrict(district)` | `Views/Shelter/ByDistrict.cshtml` | Anika |
| `ShelterController.Create/Edit` (Admin) | `Views/Shelter/Create.cshtml`, `Edit.cshtml` | Anika |
| `AdminController.AnalyticsData` (JSON) | `Views/Admin/Analytics.cshtml` (fetch + Chart.js) | Anika |
| `Views/Safety/Flood.cshtml`, `Earthquake.cshtml` | static content view | content from Anika |

### What Erin Delivers
By the Final Checkpoint: `_Layout.cshtml` + navbar/footer, Home, Login, District Alert view, Shelter Search view, two Safety Guidance pages, Admin Create/Edit forms for both Alert and Shelter, and the Chart.js Analytics dashboard — every page responsive, with graceful empty/error states.
