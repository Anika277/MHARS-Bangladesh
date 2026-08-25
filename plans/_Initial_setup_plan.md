# Initial Setup Plan — Local Development Environment

This guide gets every teammate from zero to a running local MHARS dev environment.
- Entity Framework Core **Code First** migrations manage the schema — no manual SQL scripts needed (simpler than a raw-SQL workflow given the 3-table scope).
- No external cloud services are required (unlike a RAG-heavy project) — the only outbound network call the app makes is to the public USGS API.

---

## 1. Software Prerequisites (everyone, Day 1)

### 1.1 .NET 8 SDK
1. Download from [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Install the **SDK** (not just the Runtime).
3. Verify: `dotnet --version` → should print `8.0.x`.

### 1.2 Microsoft SQL Server
- **SQL Server LocalDB** (included with Visual Studio 2022) is enough for this project — no Express install needed. Verify:
  ```
  sqllocaldb info
  sqllocaldb start MSSQLLocalDB
  ```

### 1.3 Visual Studio 2022
- Workload: **ASP.NET and web development**.
- Confirm the **.NET 8** SDK shows up under Tools → Options → Projects and Solutions.

---

## 2. Repository Branches

| Teammate | Branch | Track |
|---|---|---|
| Farin | `farin/identity-alerts` | Identity, roles, `Alert` model, alert CRUD, district filter logic |
| Anika | `anika/usgs-shelters-analytics` | USGS feed service, `Shelter` model, safety content, analytics |
| Erin | `erin/frontend-views` | Layout, all Razor Views, Bootstrap UI, Chart.js rendering |

```
git checkout <your-branch>
```

---

## 3. Configure `appsettings.Development.json`

Copy the template and fill in the local connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MHARS;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

No API keys are needed — the USGS Earthquake API is public and keyless.

> [!WARNING]
> Never commit a real `appsettings.Development.json` with machine-specific paths if teammates use different SQL Server instance names — keep a `.template` version in source control instead.

---

## 4. Database Setup (EF Core Migrations)

Unlike a manual-SQL workflow, MHARS uses Code First migrations — whoever adds/changes a model runs the migration and commits the generated files under `Data/Migrations/`; everyone else just applies them.

```
# First time / after pulling new migrations:
dotnet ef database update --project src/MHARS.Web

# After changing a model (Farin: Alert / AppUser, Anika: Shelter):
dotnet ef migrations add <DescriptiveName> --project src/MHARS.Web
dotnet ef database update --project src/MHARS.Web
```

Verify in SSMS or the Visual Studio SQL Server Object Explorer: `MHARS` database should show `AspNetUsers` (Identity), `Alert`, `Shelter`, plus the standard Identity role/claim tables.

---

## 5. Build, Run and Verify

```
dotnet restore src/MHARS.sln
dotnet build src/MHARS.sln
dotnet run --project src/MHARS.Web
```
Open `https://localhost:5001` (or the port shown in the console).

### Verification Checklist
- [ ] Home page loads with navbar and footer (Erin's layout)
- [ ] SQL Server Object Explorer shows `AppUser`(Identity)/`Alert`/`Shelter` tables
- [ ] Seeded Admin can log in (`Farin`'s seed data — check `Data/SeedData.cs` for credentials)
- [ ] District alert filter page returns results without erroring (even with empty data)
- [ ] Running with no internet still loads flood alerts/shelters — only the USGS section should show "unavailable" (NFR-2)

---

## 6. Daily Workflow Cheat Sheet

```
# 1. Pull latest
git fetch origin main
git merge origin/main

# 2. If new migrations came in, apply them
dotnet ef database update --project src/MHARS.Web

# 3. Run the app
dotnet run --project src/MHARS.Web

# 4. Commit and push your branch
git add .
git commit -m "feat: your descriptive message"
git push origin <your-branch>
```

## 7. Troubleshooting

**SQL Server: Cannot connect to `(localdb)\mssqllocaldb`**
```
sqllocaldb start MSSQLLocalDB
```

**`dotnet ef` command not found**
```
dotnet tool install --global dotnet-ef
```

**USGS fetch fails locally**
Check you have internet access and the endpoint URL in `Services/UsgsFeedService.cs` is reachable in a browser — the app should still run and show flood alerts even if this fails (that's the required behavior, not a bug).
