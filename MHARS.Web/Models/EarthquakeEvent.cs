using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MHARS.Web.Models;

/// <summary>
/// Geographic scope of a seismic event, ordered from most specific to least.
/// Stored as int. Ordering matters: containment queries use "Scope &lt;= filter".
/// </summary>
public enum RegionScope
{
    /// <summary>Inside the Bangladesh national bounding box.</summary>
    Bangladesh = 1,

    /// <summary>Within the felt radius of Bangladesh (Indo-Burman arc, Shillong plateau, Assam, north Bay of Bengal).</summary>
    Regional = 2,

    /// <summary>Elsewhere in Asia.</summary>
    Asia = 3,

    /// <summary>Rest of the world.</summary>
    Global = 4
}

/// <summary>
/// One seismic event ingested from the USGS FDSN event service.
/// Identity is the USGS event id (ExternalId) — a unique index makes ingestion idempotent.
/// </summary>
public class EarthquakeEvent
{
    public int Id { get; set; }

    /// <summary>USGS event id, e.g. "us7000nxyz". Stable across revisions of the same event.</summary>
    [Required]
    [MaxLength(64)]
    public string ExternalId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Source { get; set; } = "USGS";

    /// <summary>Nullable: USGS occasionally publishes an event before a magnitude is assigned.</summary>
    public double? Magnitude { get; set; }

    /// <summary>Magnitude scale used, e.g. "mb", "mww", "ml".</summary>
    [MaxLength(16)]
    public string MagnitudeType { get; set; } = string.Empty;

    /// <summary>USGS place description, e.g. "24 km SSE of Sylhet, Bangladesh".</summary>
    [MaxLength(256)]
    public string Place { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Focal depth in kilometres. Shallow events (&lt; 70 km) do far more surface damage.</summary>
    public double DepthKm { get; set; }

    /// <summary>Origin time, always stored UTC. Convert with BdTime.ToBst for display.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>USGS "updated" timestamp. Used to skip no-op writes when re-syncing.</summary>
    public DateTime? UsgsUpdatedAtUtc { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    public RegionScope Scope { get; set; }

    /// <summary>Great-circle distance from Dhaka, precomputed at ingest so the list view needs no maths.</summary>
    public double DistanceFromDhakaKm { get; set; }

    [MaxLength(64)]
    public string NearestDivision { get; set; } = string.Empty;

    public double NearestDivisionKm { get; set; }

    public bool TsunamiFlag { get; set; }

    /// <summary>Number of "Did You Feel It?" reports submitted to USGS.</summary>
    public int? FeltReports { get; set; }

    /// <summary>"automatic" = machine-located, not yet checked by a seismologist. "reviewed" = human-verified.</summary>
    [MaxLength(32)]
    public string ReviewStatus { get; set; } = string.Empty;

    [MaxLength(450)]
    public string DetailsUrl { get; set; } = string.Empty;

    // ---------- Presentation helpers (not persisted) ----------

    [NotMapped]
    public bool IsShallow => DepthKm < 70;

    /// <summary>USGS magnitude class descriptor.</summary>
    [NotMapped]
    public string MagnitudeBand => Magnitude switch
    {
        null => "unknown",
        < 4.0 => "minor",
        < 5.0 => "light",
        < 6.0 => "moderate",
        < 7.0 => "strong",
        _ => "major"
    };

    [NotMapped]
    public string MagnitudeBandBn => MagnitudeBand switch
    {
        "minor" => "মৃদু",
        "light" => "হালকা",
        "moderate" => "মাঝারি",
        "strong" => "তীব্র",
        "major" => "ভয়াবহ",
        _ => "অজানা"
    };

    [NotMapped]
    public string MagnitudeBandEn => MagnitudeBand switch
    {
        "minor" => "Minor",
        "light" => "Light",
        "moderate" => "Moderate",
        "strong" => "Strong",
        "major" => "Major",
        _ => "Unknown"
    };

    // ---------- Compatibility aliases for the original Home page markup ----------
    // Views/Home/Index.cshtml was written against the old service-layer type. These
    // keep it rendering unchanged. Once that view is updated to use OccurredAtUtc /
    // DetailsUrl / MagnitudeBand directly, delete this whole block.

    [NotMapped]
    public DateTime TimeUtc => OccurredAtUtc;

    [NotMapped]
    public string Url => string.IsNullOrEmpty(DetailsUrl) ? "#" : DetailsUrl;

    /// <summary>Three-way band the old home page used. Deliberately NOT the same
    /// split as MagnitudeBand — this reproduces the previous thresholds exactly so
    /// the home page cards keep their current colours.</summary>
    [NotMapped]
    public string SeverityLabel => Magnitude switch
    {
        null => "Unknown",
        < 4.0 => "Minor",
        <= 6.0 => "Moderate",
        _ => "Severe"
    };

    [NotMapped]
    public string BadgeClass => Magnitude switch
    {
        null => "secondary",
        < 4.0 => "success",
        <= 6.0 => "warning",
        _ => "danger"
    };
}