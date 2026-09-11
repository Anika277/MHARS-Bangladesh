using MHARS.Web.Models;

namespace MHARS.Web.Services;

/// <summary>
/// Pure geometry. No I/O, no database — trivially unit-testable and safe to call from
/// ingestion, from a controller, or from the risk-scoring job.
/// </summary>
public static class SeismicGeography
{
    // ---- Tier 1: Bangladesh national bounding box ----------------------------------
    // A rectangle inevitably includes slivers of West Bengal, Meghalaya, Tripura and
    // Rakhine, which is why the UI labels this tier "national territory and adjacent
    // border". To make it exact, replace IsInsideBangladesh with a point-in-polygon
    // test against a boundary GeoJSON (GADM level-0 or Natural Earth admin-0).
    public const double BdMinLat = 20.50;
    public const double BdMaxLat = 26.70;
    public const double BdMinLon = 88.00;
    public const double BdMaxLon = 92.70;

    // ---- Tier 2: regional seismic source zone --------------------------------------
    // This was a 700 km circle centred on Bangladesh, and that was the wrong shape.
    // The source zones that generate Bangladesh's hazard — the Indo-Burman subduction
    // wedge, the Dauki fault and Shillong plateau, the Assam valley, and the Himalayan
    // frontal thrust — form a broad rectangle, not a disc. A circle clips exactly the
    // corners where the Indo-Burman arc sits: a M4.2 at 26.38N 96.64E measured 700.8 km
    // from the centroid and fell out of the tier by 800 metres.
    //
    // The box below is the envelope of those zones as used in Bangladesh seismic hazard
    // literature and the BNBC zoning discussion. VERIFY the exact bounds against a cited
    // source before they go in the report.
    public const double RegionalMinLat = 18.00;
    public const double RegionalMaxLat = 30.00;
    public const double RegionalMinLon = 85.00;
    public const double RegionalMaxLon = 98.00;

    // ---- Tier 3: Asia ---------------------------------------------------------------
    // Extends to the dateline, so the western Pacific island arcs (Guam, the Marianas)
    // fall inside it. Conventionally those are Oceania, not Asia; no rectangle can
    // separate them from Japan, so the footnote on the page says so plainly.
    public const double AsiaMinLat = -11.0;
    public const double AsiaMaxLat = 82.0;
    public const double AsiaMinLon = 25.0;
    public const double AsiaMaxLon = 180.0;

    public const double DhakaLat = 23.8103;
    public const double DhakaLon = 90.4125;

    private const double EarthRadiusKm = 6371.0088;

    /// <summary>
    /// Divisional headquarters, used to answer "where in Bangladesh is this, roughly?".
    /// VERIFY these coordinates against an authoritative source (BBS / Survey of
    /// Bangladesh) before quoting them in the report.
    /// </summary>
    public static readonly IReadOnlyList<Division> Divisions = new List<Division>
    {
        new("Dhaka",       "ঢাকা",        23.8103, 90.4125),
        new("Chattogram",  "চট্টগ্রাম",    22.3569, 91.7832),
        new("Khulna",      "খুলনা",       22.8456, 89.5403),
        new("Rajshahi",    "রাজশাহী",     24.3745, 88.6042),
        new("Sylhet",      "সিলেট",       24.8949, 91.8687),
        new("Barishal",    "বরিশাল",      22.7010, 90.3535),
        new("Rangpur",     "রংপুর",       25.7439, 89.2752),
        new("Mymensingh",  "ময়মনসিংহ",   24.7471, 90.4203)
    };

    public sealed record Division(string Name, string NameBn, double Lat, double Lon);

    /// <summary>
    /// Assigns scope from coordinates alone — never from which feed returned the event.
    /// The same quake appears in more than one query; classifying by geometry keeps the
    /// answer identical every time. Cascade runs most-specific first.
    /// </summary>
    public static RegionScope Classify(double lat, double lon)
    {
        if (IsInsideBangladesh(lat, lon)) return RegionScope.Bangladesh;
        if (IsInsideRegional(lat, lon)) return RegionScope.Regional;
        if (IsInsideAsia(lat, lon)) return RegionScope.Asia;
        return RegionScope.Global;
    }

    /// <summary>
    /// UPGRADE HOOK: swap the body for a ray-casting point-in-polygon test once a
    /// Bangladesh boundary GeoJSON is loaded. Nothing else needs to change.
    /// </summary>
    public static bool IsInsideBangladesh(double lat, double lon)
        => lat >= BdMinLat && lat <= BdMaxLat && lon >= BdMinLon && lon <= BdMaxLon;

    public static bool IsInsideRegional(double lat, double lon)
        => lat >= RegionalMinLat && lat <= RegionalMaxLat
        && lon >= RegionalMinLon && lon <= RegionalMaxLon;

    public static bool IsInsideAsia(double lat, double lon)
        => lat >= AsiaMinLat && lat <= AsiaMaxLat && lon >= AsiaMinLon && lon <= AsiaMaxLon;

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// Straight-line distance from the surface point to the focus, accounting for depth.
    /// Ground shaking attenuates with hypocentral distance, not epicentral distance —
    /// a 100 km deep quake directly underfoot is 100 km away, not zero.
    /// </summary>
    public static double HypocentralKm(double lat1, double lon1, double lat2, double lon2, double depthKm)
    {
        var epi = HaversineKm(lat1, lon1, lat2, lon2);
        return Math.Sqrt(epi * epi + depthKm * depthKm);
    }

    public static double DistanceFromDhakaKm(double lat, double lon)
        => HaversineKm(lat, lon, DhakaLat, DhakaLon);

    public static (string Name, double Km) NearestDivision(double lat, double lon)
    {
        var best = Divisions[0];
        double bestKm = HaversineKm(lat, lon, best.Lat, best.Lon);

        for (int i = 1; i < Divisions.Count; i++)
        {
            double km = HaversineKm(lat, lon, Divisions[i].Lat, Divisions[i].Lon);
            if (km < bestKm)
            {
                bestKm = km;
                best = Divisions[i];
            }
        }

        return (best.Name, bestKm);
    }

    public static string BanglaNameFor(string divisionName)
        => Divisions.FirstOrDefault(d => d.Name == divisionName)?.NameBn ?? divisionName;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Bangladesh Standard Time (UTC+6, no daylight saving).
/// USGS publishes UTC; showing UTC on a Bangladeshi public-safety site is a correctness
/// bug, not a cosmetic one — a 03:00 UTC quake is 09:00 in the morning here.
/// </summary>
public static class BdTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Bangladesh Standard Time", "Asia/Dhaka" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("BST+6", TimeSpan.FromHours(6), "Bangladesh Standard Time", "BST");
    }

    public static DateTime ToBst(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, Zone);
    }

    public static DateTime Now => ToBst(DateTime.UtcNow);

    public static string Relative(DateTime utc)
    {
        var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
        return $"{(int)span.TotalDays} d ago";
    }
}