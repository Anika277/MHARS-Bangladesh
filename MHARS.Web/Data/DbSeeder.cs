using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Models;

namespace MHARS.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();

        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole(adminRole));

        const string adminEmail = "admin@mhars.gov.bd";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, adminRole);
        }

        if (!await db.Shelters.AnyAsync())
        {
            db.Shelters.AddRange(
                new Shelter { Name = "Dhaka College Shelter", District = "Dhaka", Address = "Mirpur Road, Dhanmondi, Dhaka", Capacity = 1200, ContactNumber = "02-9611234" },
                new Shelter { Name = "Sir Salimullah Hall", District = "Dhaka", Address = "Nawabpur Road, Dhaka", Capacity = 800, ContactNumber = "02-7311901" },
                new Shelter { Name = "Sylhet Govt. Pilot High School", District = "Sylhet", Address = "Chowhatta Point, Sylhet", Capacity = 900, ContactNumber = "0821-720014" },
                new Shelter { Name = "Chattogram Collegiate School", District = "Chattogram", Address = "Ice Factory Road, Chattogram", Capacity = 1000, ContactNumber = "031-619400" },
                new Shelter { Name = "Khulna Zila School Ground", District = "Khulna", Address = "Khan Jahan Ali Road, Khulna", Capacity = 1500, ContactNumber = "041-720010" },
                new Shelter { Name = "Gaibandha Govt. Boys High School", District = "Gaibandha", Address = "College Road, Gaibandha", Capacity = 700, ContactNumber = "0541-55012" },
                new Shelter { Name = "Kurigram Govt. High School", District = "Kurigram", Address = "Station Road, Kurigram", Capacity = 650, ContactNumber = "0581-62011" },
                new Shelter { Name = "Cox's Bazar Govt. High School", District = "Cox's Bazar", Address = "Laldighi, Cox's Bazar", Capacity = 1100, ContactNumber = "0341-63021" }
            );
        }

        if (!await db.Alerts.AnyAsync())
        {
            db.Alerts.AddRange(
                new Alert { HazardType = HazardType.Flood, District = "Sirajganj", Title = "Jamuna River Inundation", Message = "Water level at Jamuna River Sirajganj point is 13.83m (+48cm over danger mark). Embankment seepage reported at Kazipur and Belkuchi. Residents of char areas are advised to relocate to the nearest shelter.", Severity = SeverityLevel.High, SourceReference = "FFWC Bulletin #48", IssuedBy = "Authority" },
                new Alert { HazardType = HazardType.Flood, District = "Kurigram", Title = "Brahmaputra Overflow", Message = "Brahmaputra at Chilmari flowing +52cm above danger level. Chilmari and Nageshwari lowlands flooded across 15 unions. Safe drinking water tube-wells submerged.", Severity = SeverityLevel.High, SourceReference = "FFWC Bulletin #47", IssuedBy = "Authority" },
                new Alert { HazardType = HazardType.Earthquake, District = "Sylhet", Title = "M 4.6 Seismic Tremor", Message = "Epicenter 38km ESE of Sylhet at 12.4km depth. Tremor felt across Sylhet city. No damage reported. Aftershocks possible.", Severity = SeverityLevel.Medium, SourceReference = "USGS", IssuedBy = "USGS Auto" },
                new Alert { HazardType = HazardType.Flood, District = "Sunamganj", Title = "Surma River Surge", Message = "Surma River rising +12cm in 4 hours due to Meghalaya runoff. Haor basin residents advised to stay alert.", Severity = SeverityLevel.Medium, SourceReference = "FFWC Bulletin #46", IssuedBy = "Authority" },
                new Alert { HazardType = HazardType.Flood, District = "Dhaka", Title = "Normal Baseline", Message = "Buriganga & Turag rivers within normal seasonal limits. No action required.", Severity = SeverityLevel.Low, SourceReference = "FFWC", IssuedBy = "Authority" }
            );
        }

        if (!await db.SafetyGuidelines.AnyAsync())
        {
            db.SafetyGuidelines.AddRange(
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = true, SortOrder = 1, Text = "Move to higher ground or the nearest shelter as soon as a flood warning is issued." },
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = true, SortOrder = 2, Text = "Keep drinking water, dry food, torchlight, radio and first-aid supplies ready." },
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = true, SortOrder = 3, Text = "Switch off electricity at the main switch before leaving your home." },
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = true, SortOrder = 4, Text = "Keep important documents in waterproof bags." },
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = false, SortOrder = 5, Text = "Never walk or drive through moving floodwater — 15 cm of moving water can knock you down." },
                new SafetyGuideline { HazardType = HazardType.Flood, IsDo = false, SortOrder = 6, Text = "Do not touch electrical equipment with wet hands or while standing in water." },
                new SafetyGuideline { HazardType = HazardType.Earthquake, IsDo = true, SortOrder = 1, Text = "Drop, Cover, and Hold On — get under a sturdy table until shaking stops." },
                new SafetyGuideline { HazardType = HazardType.Earthquake, IsDo = true, SortOrder = 2, Text = "Move to open ground away from buildings, trees and power lines after shaking stops." },
                new SafetyGuideline { HazardType = HazardType.Earthquake, IsDo = true, SortOrder = 3, Text = "Keep shoes and a torch near your bed for night-time quakes." },
                new SafetyGuideline { HazardType = HazardType.Earthquake, IsDo = false, SortOrder = 4, Text = "Never use lifts during or immediately after an earthquake." },
                new SafetyGuideline { HazardType = HazardType.Earthquake, IsDo = false, SortOrder = 5, Text = "Do not stand near windows, mirrors, bookcases or hanging objects." }
            );
        }

        await db.SaveChangesAsync();
    }
}
