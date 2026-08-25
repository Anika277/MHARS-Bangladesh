# MHARS — Functional & Non-Functional Requirements

---

## 1. Functional Requirements (FR-1 through FR-9)

| ID | Requirement | Owner Track |
|---|---|---|
| **FR-1** | An Admin can register/log in via ASP.NET Identity (Admin accounts are seeded/promoted, not self-service sign-up). | Farin |
| **FR-2** | An Admin can create, edit, and deactivate a district-wise flood Alert with a severity level (Low/Medium/High), referencing an official bulletin in free text. | Farin |
| **FR-3** | The system automatically fetches recent earthquake events from the USGS Earthquake API and stores them as `Alert` rows with `Source = USGS`, auto-colored by magnitude. | Anika |
| **FR-4** | A Citizen (no login required) can filter and view current alerts — both flood and earthquake — for a selected district. | Erin (view) + Farin (query) |
| **FR-5** | A Citizen can search/browse the Shelter directory (name, district, address, capacity, contact number), filterable by district. | Anika (data) + Erin (view) |
| **FR-6** | A Citizen can read static, plain-language Do's and Don'ts safety guidance pages for floods and earthquakes. | Anika (content) + Erin (view) |
| **FR-7** | An Admin can manage the Shelter directory (create/edit/remove shelter entries). | Anika |
| **FR-8** | An Admin can view an analytics dashboard chart (Chart.js) summarizing alerts issued per district/month, built from the `Alert` table. | Anika (data endpoint) + Erin (chart rendering) |
| **FR-9** | Every page renders with a consistent responsive layout (Bootstrap 5) usable on mobile, since citizens may check hazard status from a phone during an emergency. | Erin |

---

## 2. Non-Functional Requirements (NFR)

- **NFR-1 (Usability):** Citizen-facing pages require no technical background or account — the core "is my district safe" flow must be reachable within 2 clicks from the homepage.
- **NFR-2 (Availability of core flow):** If the USGS API is unreachable, the app must still show flood alerts and shelters — earthquake data degrades gracefully (e.g., "Live earthquake feed temporarily unavailable"), it must not crash the page.
- **NFR-3 (Performance):** USGS data is fetched on a schedule/cache (not per-request) so page loads are not blocked on an external API call.
- **NFR-4 (Data integrity):** Only authenticated Admins can write to `Alert` (manual entries) and `Shelter`; Citizen/guest traffic is strictly read-only.
- **NFR-5 (Honesty of scope):** UI copy must not imply predictive or automatic hazard detection beyond what FR-1–FR-9 actually deliver.
- **NFR-6 (Course constraint):** All data access goes through Entity Framework Core Code First against Microsoft SQL Server, run inside Visual Studio, per the course requirement.

---

## 3. Explicitly Out of Scope (future work, not implemented)

Deliberately excluded so the 3-member team is not building beyond a one-semester course project. Still worth stating in the report to show ambition without over-promising:

- Real-time push notifications (SignalR).
- SMS alerts via a paid gateway (e.g., Twilio).
- Crowd-sourced citizen incident reporting with photo upload.
- Any automatic flood-level sensing or earthquake *prediction* — no credible free data source or hardware exists for this within the project's scope, and claiming prediction capability would be scientifically inaccurate.
- A normalized `District` lookup table (District/Shelter are linked by matching text values only — documented trade-off).

---

## 4. Checkpoint-to-Requirement Mapping

| Checkpoint | Course Deliverable | Requirements Delivered |
|---|---|---|
| **Checkpoint 1** | Full-stack ASP.NET Core MVC foundation | FR-1 (Identity/roles), schema + migrations, seed data, basic scaffolded views for all 3 tracks |
| **Checkpoint 2** | Review Class (in-class progress review) | FR-2, FR-3, FR-4, FR-5, FR-6, FR-7 fully working end-to-end |
| **Final Project Checkpoint** | Presentation on project | FR-8, FR-9, polish, bug fixes, demo script, all FRs verified live |
