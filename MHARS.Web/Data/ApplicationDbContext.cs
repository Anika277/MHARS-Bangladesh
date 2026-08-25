using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Models;

namespace MHARS.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<Shelter> Shelters { get; set; }
    public DbSet<SafetyGuideline> SafetyGuidelines { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Alert>().Property(a => a.HazardType).HasConversion<string>();
        builder.Entity<Alert>().Property(a => a.Severity).HasConversion<string>();
        builder.Entity<SafetyGuideline>().Property(g => g.HazardType).HasConversion<string>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<string>().HaveMaxLength(450);
    }
}
