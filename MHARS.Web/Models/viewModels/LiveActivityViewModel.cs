using MHARS.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace MHARS.Web.Models.ViewModels;

public class LiveActivityViewModel
{
    public List<IdentityUser> RecentUsers { get; set; } = new();
    public List<Alert> RecentAlerts { get; set; } = new();
    public List<Shelter> RecentShelters { get; set; } = new();
    public List<SafetyGuideline> RecentGuidelines { get; set; } = new();
    public List<EarthquakeEvent> RecentEarthquakes { get; set; } = new();
    public DateTime LastServerCheck { get; set; }
}