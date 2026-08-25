# MHARS — Execution Plan & Task Breakdown

Maps the 3 course checkpoints onto an equal, 3-way work split. Each member owns one track end-to-end (own tables/services on the backend side, or the full view layer on the frontend side) so grading of individual contribution is unambiguous, while still needing the other two to integrate for a working app — same shape as the branch-per-member workflow in `plans/_Initial_setup_plan.md`.

---

## 1. Checkpoint Summary

| Checkpoint | Course Deliverable | Focus Area | Exit Criteria |
|---|---|---|---|
| **Checkpoint 1** | Full Stack ASP.NET Core MVC Web Development | Solution scaffolding, 3-table EF Core schema + migrations, ASP.NET Identity roles, seed data, one scaffolded view per track | Solution builds and runs; `Admin`/`Citizen` roles work; SQL Server shows `AppUser`, `Alert`, `Shelter` tables; a seeded Admin can log in; every member has at least one working controller+view. |
| **Checkpoint 2** | Review Class | Core citizen + admin flows working end-to-end | Admin can CRUD flood alerts and shelters; USGS earthquake feed populates real `Alert` rows; citizen can filter alerts and shelters by district; safety guidance pages render; all 3 members' work integrated on `main`. |
| **Final Project Checkpoint** | Presentation on project | Analytics dashboard, responsive polish, bug fixes, demo readiness | Chart.js dashboard renders alert counts per district/month; all pages responsive on mobile; no crashes when USGS is unreachable; live demo script rehearsed. |

---

## 2. Equal 3-Way Task Split

Split by **vertical ownership** — each person owns a track across all 3 checkpoints, rather than everyone touching everything every sprint.

### Farin — Identity, Alerts & Data Layer (`farin/identity-alerts`)
- [ ] CP1: Scaffold the solution; set up `AppDbContext`, EF Core Code First migrations; configure ASP.NET Identity with `Admin`/`Citizen` roles; seed one Admin user.
- [ ] CP1: Implement `Alert` model + migration.
- [ ] CP2: `AlertController` — Admin CRUD (create/edit/deactivate flood alerts with severity); Citizen district-filter query (feeds FR-4 alongside Erin's view).
- [ ] CP2: District filtering logic shared by both Alert and Shelter lookups (plain text match, per design.md §2.2).
- [ ] Final: Bug-fix pass on alert CRUD; verify Admin-only write access (NFR-4) with `[Authorize(Roles="Admin")]`.

### Anika — External Data, Shelters & Analytics (`anika/usgs-shelters-analytics`)
- [ ] CP1: Implement `Shelter` model + migration; write safety guidance content (Do's/Don'ts copy) for floods and earthquakes.
- [ ] CP1: Stub `IUsgsFeedService` interface and a `HttpClient`-based skeleton.
- [ ] CP2: Finish `UsgsFeedService` — fetch, region-filter, magnitude-based severity, upsert into `Alert` with `Source = USGS`; handle API failure gracefully (NFR-2).
- [ ] CP2: `ShelterController` — Admin CRUD + citizen district-filtered search.
- [ ] Final: `AnalyticsService` — aggregate alert counts per district/month for Chart.js (FR-8 data side); verify counts against seeded/live data.

### Erin — Frontend Views & UI (`erin/frontend-views`)
- [ ] CP1: `_Layout.cshtml` master layout, navbar, footer; Bootstrap 5 wired into `wwwroot`; Home/Index landing page; Login/Register views for Identity (Admin only needs to log in — no citizen sign-up flow needed unless the team wants one).
- [ ] CP1: Mock data / ViewModel stubs so frontend work isn't blocked waiting on Farin's and Anika's controllers (see `plans/Erin_plan.md` §Setup).
- [ ] CP2: District-wise Alert view (flood + earthquake together, FR-4); Shelter search/browse view (FR-5); static Safety Guideline pages (FR-6) using Anika's content.
- [ ] CP2: Admin views for Alert CRUD and Shelter CRUD forms (wired to Farin's/Anika's controllers).
- [ ] Final: Chart.js analytics dashboard view (FR-8) consuming Anika's `AnalyticsService` endpoint; full responsive pass (FR-9) across all pages; polish and empty/error states (e.g. "feed unavailable" banner).

Each track is roughly one-third of FR-1–FR-9 and touches CP1/CP2/Final equally — nobody is front-loaded or back-loaded.

---

## 3. Target Repository File Structure

```
MHARS-Bangladesh/
├── MHARS.sln
├── README.md
├── AGENTS.md
├── .agent/
│   └── spec/
│       ├── requirements.md
│       ├── design.md
│       └── tasks.md
├── plans/
│   ├── _Initial_setup_plan.md
│   ├── Farin_plan.md
│   ├── Anika_plan.md
│   └── Erin_plan.md
└── src/
    └── MHARS.Web/
        ├── Controllers/
        ├── Models/
        │   └── ViewModels/
        ├── Data/
        │   └── Migrations/
        ├── Services/
        ├── Views/
        │   ├── Home/
        │   ├── Alert/
        │   ├── Shelter/
        │   ├── Safety/
        │   ├── Admin/
        │   ├── Account/
        │   └── Shared/
        └── wwwroot/
            ├── css/
            ├── js/
            └── lib/
```
