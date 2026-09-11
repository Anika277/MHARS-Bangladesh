namespace MHARS.Web.Models.ViewModels;

public class EarthquakeDashboardViewModel
{
    public RegionScope ActiveScope { get; set; } = RegionScope.Bangladesh;

    public IReadOnlyList<EarthquakeEvent> Events { get; set; } = Array.Empty<EarthquakeEvent>();

    public IReadOnlyList<ScopeTab> Tabs { get; set; } = Array.Empty<ScopeTab>();

    public int LookbackDays { get; set; }

    public int CountLast24Hours { get; set; }

    public int CountLast7Days { get; set; }

    public EarthquakeEvent? Strongest { get; set; }

    public EarthquakeEvent? MostRecent { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public string? SyncError { get; set; }

    /// <summary>Pre-serialised marker data so the Razor view stays free of serialisation logic.</summary>
    public string MapPointsJson { get; set; } = "[]";

    public bool HasData => Events.Count > 0;
}

public sealed record ScopeTab(
    RegionScope Scope,
    string Key,
    string LabelBn,
    string LabelEn,
    string Caption,
    int Count);
