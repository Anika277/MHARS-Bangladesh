using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Models;

namespace MHARS.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<SafetyGuideline> SafetyGuidelines => Set<SafetyGuideline>();
    public DbSet<EarthquakeEvent> EarthquakeEvents => Set<EarthquakeEvent>();

    // ONE OnModelCreating only. C# allows no second method with the same signature,
    // and even if it did, EF calls this exactly once — a second copy would silently
    // never run. Every entity's configuration goes inside this single method.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // must stay first: this builds the Identity tables

        builder.Entity<Alert>().Property(a => a.HazardType).HasConversion<string>();
        builder.Entity<Alert>().Property(a => a.Severity).HasConversion<string>();
        builder.Entity<SafetyGuideline>().Property(g => g.HazardType).HasConversion<string>();

        builder.Entity<EarthquakeEvent>(e =>
        {
            // Makes ingestion idempotent: re-running the sync cannot duplicate an event.
            e.HasIndex(x => x.ExternalId).IsUnique();

            // Covers the tab queries: WHERE Scope <= @scope AND OccurredAtUtc >= @since
            e.HasIndex(x => new { x.Scope, x.OccurredAtUtc });

            // Deliberately NOT .HasConversion<string>(), unlike Alert.HazardType above.
            // RegionScope must stay an int, because the controller filters with
            // "Scope <= activeScope". Stored as text, SQL Server would compare
            // alphabetically — 'Asia' < 'Bangladesh' < 'Global' < 'Regional' — and the
            // Bangladesh tab would quietly start showing Asian earthquakes.
            e.Property(x => x.Scope).HasConversion<int>();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<string>().HaveMaxLength(450);
    }
}