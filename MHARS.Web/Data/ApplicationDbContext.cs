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
    public DbSet<DonationCampaign> DonationCampaigns => Set<DonationCampaign>();
    public DbSet<Donation> Donations => Set<Donation>();

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

        // ---------- Relief Fund ----------
        builder.Entity<DonationCampaign>(e =>
        {
            e.Property(x => x.GoalAmount).HasPrecision(18, 2);   // money: never float/double
            e.Property(x => x.HazardType).HasConversion<string>();
        });

        builder.Entity<Donation>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>();   // readable in SSMS: 'Paid', 'Pending'...

            e.HasIndex(x => x.TranId).IsUnique();                // one row per gateway transaction
            e.HasIndex(x => x.ReceiptToken).IsUnique();          // receipt lookup
            e.HasIndex(x => new { x.Status, x.CampaignId });     // tracker: WHERE Status='Paid' GROUP BY CampaignId

            e.Ignore(x => x.ReceiptNumber);                      // computed in C#, not a column

            // Real FK (unlike Alert/Shelter's District text match): a donation must belong to
            // an existing campaign. Restrict = a campaign with donations can't be deleted by accident.
            e.HasOne(x => x.Campaign)
             .WithMany(c => c.Donations)
             .HasForeignKey(x => x.CampaignId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<string>().HaveMaxLength(450);
    }
}