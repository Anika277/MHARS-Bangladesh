# MHARS (Multi-Hazard Alert & Response System) — Agent Guidelines & Project Overview

Welcome to the MHARS codebase. This document is the core instruction and boundary manual for AI coding agents (Antigravity, Copilot, Claude, etc.) working on this repository. Read this file first, then `.agent/spec/` for full detail, then your teammate's file in `plans/`.

---

## 1. Project Mission & Identity

**MHARS** is a citizen-facing multi-hazard alert platform for Bangladesh.
- **Citizen Experience:** Any citizen — no account required — can check whether their district currently has an active flood or earthquake alert, browse verified emergency shelters near them, and read plain-language safety guidance for floods and earthquakes.
- **Admin Experience:** A verified Admin/Authority account issues and manages district-wise flood alerts (referencing official BMD/FFWC bulletins) and maintains the shelter directory. Earthquake alerts are not manually written — they are pulled automatically from the live USGS Earthquake API.
- **Academic Context:** CSE 3200 — Software Development V, AUST, Fall 2025. Course requires ASP.NET Core MVC + Entity Framework + Microsoft SQL Server in Visual Studio. Delivered across 3 checkpoints (see `.agent/spec/tasks.md`).
- **Scope Discipline:** This is a **3-member, one-semester, beginner** project. No sensor networks, no prediction models, no SMS/push notifications, no crowd-sourced reporting. See "Explicitly out of scope" in `.agent/spec/requirements.md`. Do not add speculative features beyond what is listed there — agents should flag scope creep, not silently implement it.

---

## 2. Technology Stack & Frameworks

| Layer | Choice | Rationale & Boundaries |
|---|---|---|
| **Backend Framework** | ASP.NET Core MVC (.NET 8) | Course-mandated stack |
| **Language** | C# (.NET 8) | Course language |
| **Data Access** | Entity Framework Core (Code First) | `DbContext`, LINQ queries, `Add-Migration` / `Update-Database` |
| **Primary Relational DB** | Microsoft SQL Server (LocalDB or Express) | 3-table schema: `AppUser` (Identity), `Alert`, `Shelter` |
| **Authentication** | ASP.NET Core Identity | Roles: `Admin`, `Citizen` (Citizen browsing does not require login — Identity is only for the Admin gate) |
| **Frontend** | Razor Views + Bootstrap 5 | Server-rendered MVC. No SPA framework (React/Vue/Angular) — out of course scope |
| **External Data** | USGS Earthquake Hazards Program REST API (GeoJSON, no key) | Fetched server-side via `HttpClient`, never called directly from the browser |
| **Charts** | Chart.js | Admin analytics dashboard only |
| **IDE** | Microsoft Visual Studio 2022 | Course-mandated |
| **Version Control** | Git / GitHub, 3-member workflow | See `plans/_Initial_setup_plan.md` for branch names |

---

## 3. Strict Architectural Rules & Boundaries

1. **Project Layout:** A single ASP.NET Core MVC project is sufficient for this scope (unlike a multi-project Clean Architecture split) — keep `Models/`, `Data/`, `Services/`, `Controllers/`, `Views/` folders inside one `MHARS.Web` project. Do not over-engineer a multi-project solution for a 3-table schema.
2. **Schema Integrity (3 tables, intentionally minimal):**
   - `AppUser` (ASP.NET Identity `IdentityUser` extended with `Role`) — Admin or Citizen. Citizen accounts are optional; most citizen traffic is anonymous/guest.
   - `Alert` — district-wise hazard alerts. `HazardType` (Flood/Earthquake), `Severity` (Low/Medium/High), `District` (text match, not a FK — documented trade-off, see design.md), `Source` (Manual/USGS), plus earthquake-only fields (`Magnitude`, `Depth`, `Latitude`, `Longitude`) left nullable for flood rows.
   - `Shelter` — Admin-managed directory: `Name`, `District`, `Address`, `Capacity`, `ContactNumber`.
   - **District and Shelter are linked by matching text values, not a FK** — this is a deliberate, documented simplification (see Known Limitations in the full proposal). Do not "fix" this by adding a `District` lookup table unless explicitly asked — it changes the agreed schema all 3 members are building against.
3. **USGS Integration Boundary:**
   - Earthquake data is **read-only and never written back to USGS**. It is fetched via `HttpClient`, mapped into `Alert` rows with `Source = "USGS"`, and cached/refreshed on a schedule (not on every page load) so the app doesn't hammer the public API.
   - Region filtering (South/Southeast Asia bounding box) happens server-side after the fetch, not via USGS query parameters the team hasn't verified.
4. **Alert Trust Model:** Flood alerts are **Admin-authored, not system-verified** — the Admin references an official bulletin and manually creates the alert. The app does not claim to independently validate flood data. Earthquake severity coloring (e.g. `<4.0` minor, `4–6` moderate, `>6` severe) is computed from the USGS magnitude field, not hand-entered.
5. **No Overclaiming:** Do not add copy, UI badges, or code comments implying the system predicts hazards, senses them automatically, or guarantees shelter availability. This mirrors the "Why This Approach" honesty framing in the proposal and matters for grading.

---

## 4. Structured Specification Reference

Detailed project knowledge is maintained in `.agent/spec/`:
- [Requirements & Specifications](file:///.agent/spec/requirements.md) — Functional requirements (FR-1 to FR-9), NFRs, out-of-scope list.
- [Architecture & Database Design](file:///.agent/spec/design.md) — Single-project structure, 3-entity schema, USGS integration flow.
- [Execution Plan & Tasks](file:///.agent/spec/tasks.md) — 3-checkpoint breakdown mapped to the course schedule, equal 3-way task split, target repository file layout.

Per-member implementation plans are in `plans/`:
- [Initial Setup](file:///plans/_Initial_setup_plan.md) — shared onboarding: SDK, SQL Server, branches, `appsettings.Development.json`.
- [Erin — Frontend Plan](file:///plans/Erin_plan.md) — Razor Views, Bootstrap UI, Chart.js rendering, responsive layout.
- [Farin — Identity & Alerts Plan](file:///plans/Farin_plan.md) — Auth, Admin alert CRUD, district filtering logic, DbContext/migrations.
- [Anika — External Data & Shelters Plan](file:///plans/Anika_plan.md) — USGS integration service, Shelter directory, safety guideline content, analytics data.

**Team:** Farin Maisha (20230104001) · Sanjida Amin Erin (20230104016) · Anika Sultana (20230104018) — Group A1, submitted to Tanjila Broti, Dept. of CSE, AUST.
