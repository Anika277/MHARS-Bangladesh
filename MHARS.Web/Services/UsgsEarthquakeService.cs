using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Services;

// =====================================================================================
//  Options
// =====================================================================================

public class UsgsOptions
{
    /// <summary>
    /// FDSN event service. Note this is the *query* endpoint, not the pre-baked GeoJSON
    /// summary feeds — it accepts bounding boxes, radii and magnitude floors as parameters,
    /// which is what lets us pull each scope at its own sensitivity.
    /// </summary>
    public string BaseUrl { get; set; } = "https://earthquake.usgs.gov/fdsnws/event/1/query";

    public int SyncIntervalMinutes { get; set; } = 15;
    public int LookbackDays { get; set; } = 30;

    public double BangladeshMinMagnitude { get; set; } = 2.5;
    public double RegionalMinMagnitude { get; set; } = 4.0;
    public double AsiaMinMagnitude { get; set; } = 4.5;

    /// <summary>Manual refresh throttle, so a demo audience clicking the button cannot hammer USGS.</summary>
    public int ManualRefreshCooldownSeconds { get; set; } = 60;

    /// <summary>USGS asks API consumers to identify themselves.</summary>
    public string UserAgent { get; set; } = "MHARS-Bangladesh/1.0 (AUST CSE 3200 academic project)";

    public int MaxRecordsPerQuery { get; set; } = 2000;
}

// =====================================================================================
//  Sync status (singleton — survives between scoped service instances)
// =====================================================================================

public class UsgsSyncStatus
{
    private readonly object _lock = new();

    public DateTime? LastAttemptUtc { get; private set; }
    public DateTime? LastSuccessUtc { get; private set; }
    public string? LastError { get; private set; }
    public int LastInserted { get; private set; }
    public int LastUpdated { get; private set; }

    /// <summary>Set by the manual Refresh button only — never by the background timer.</summary>
    public DateTime? LastManualUtc { get; private set; }

    public void RecordAttempt()
    {
        lock (_lock) { LastAttemptUtc = DateTime.UtcNow; }
    }

    public void RecordManualRefresh()
    {
        lock (_lock) { LastManualUtc = DateTime.UtcNow; }
    }

    public void RecordSuccess(int inserted, int updated)
    {
        lock (_lock)
        {
            LastSuccessUtc = DateTime.UtcNow;
            LastInserted = inserted;
            LastUpdated = updated;
            LastError = null;
        }
    }

    public void RecordFailure(string error)
    {
        lock (_lock) { LastError = error; }
    }

    /// <summary>
    /// Seconds still to wait before another MANUAL refresh is allowed; 0 when ready.
    /// Deliberately measured against LastManualUtc, not LastAttemptUtc: the background
    /// timer fires every few minutes, and throttling the button against the timer meant
    /// a user pressing Refresh was almost always told to wait for something they did
    /// not do.
    /// </summary>
    public int ManualCooldownRemaining(int cooldownSeconds)
    {
        lock (_lock)
        {
            if (LastManualUtc is null) return 0;

            var elapsed = (DateTime.UtcNow - LastManualUtc.Value).TotalSeconds;
            return elapsed >= cooldownSeconds ? 0 : (int)Math.Ceiling(cooldownSeconds - elapsed);
        }
    }
}

public sealed record UsgsSyncResult(int Fetched, int Inserted, int Updated, string? Error)
{
    public bool Succeeded => Error is null;
}

// =====================================================================================
//  Service
// =====================================================================================

public class UsgsEarthquakeService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ApplicationDbContext _db;
    private readonly UsgsSyncStatus _status;
    private readonly UsgsOptions _options;
    private readonly ILogger<UsgsEarthquakeService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public UsgsEarthquakeService(
        IHttpClientFactory httpFactory,
        ApplicationDbContext db,
        UsgsSyncStatus status,
        IOptions<UsgsOptions> options,
        ILogger<UsgsEarthquakeService> logger)
    {
        _httpFactory = httpFactory;
        _db = db;
        _status = status;
        _options = options.Value;
        _logger = logger;
    }

    public UsgsOptions Options => _options;
    public UsgsSyncStatus Status => _status;

    /// <summary>
    /// Pulls all three scopes, de-duplicates, classifies by geometry and upserts.
    /// Safe to run repeatedly — ExternalId is the natural key.
    /// </summary>
    public async Task<UsgsSyncResult> SyncAsync(CancellationToken ct = default)
    {
        _status.RecordAttempt();

        try
        {
            var start = DateTime.UtcNow.AddDays(-_options.LookbackDays);

            // Labelled so the log says which of the three queries came back empty.
            // Without this, "Bangladesh 0 / Regional 0 / Asia 413" is indistinguishable
            // from a classification bug, and you cannot tell which to go and look at.
            var queries = new (string Label, string Url)[]
            {
                ("Bangladesh-bbox", BuildBoundingBoxUrl(start, _options.BangladeshMinMagnitude,
                    SeismicGeography.BdMinLat, SeismicGeography.BdMaxLat,
                    SeismicGeography.BdMinLon, SeismicGeography.BdMaxLon)),

                ("Regional-bbox", BuildBoundingBoxUrl(start, _options.RegionalMinMagnitude,
                    SeismicGeography.RegionalMinLat, SeismicGeography.RegionalMaxLat,
                    SeismicGeography.RegionalMinLon, SeismicGeography.RegionalMaxLon)),

                ("Asia-bbox", BuildBoundingBoxUrl(start, _options.AsiaMinMagnitude,
                    SeismicGeography.AsiaMinLat, SeismicGeography.AsiaMaxLat,
                    SeismicGeography.AsiaMinLon, SeismicGeography.AsiaMaxLon))
            };

            var collected = new Dictionary<string, EarthquakeEvent>(StringComparer.Ordinal);

            foreach (var (label, url) in queries)
            {
                var batch = await FetchAsync(url, ct);

                _logger.LogInformation("USGS {Label}: {Count} events returned. {Url}",
                    label, batch.Count, url);

                foreach (var evt in batch)
                {
                    // First writer wins. Scope came from geometry, so duplicates are identical.
                    collected.TryAdd(evt.ExternalId, evt);
                }
            }

            foreach (var g in collected.Values.GroupBy(e => e.Scope).OrderBy(g => g.Key))
                _logger.LogInformation("Classified {Count} events as {Scope}.", g.Count(), g.Key);

            var (inserted, updated) = await UpsertAsync(collected.Values.ToList(), ct);

            _status.RecordSuccess(inserted, updated);
            _logger.LogInformation("USGS sync complete. Fetched {Fetched}, inserted {Ins}, updated {Upd}.",
                collected.Count, inserted, updated);

            return new UsgsSyncResult(collected.Count, inserted, updated, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "USGS sync failed.");
            _status.RecordFailure(ex.Message);
            return new UsgsSyncResult(0, 0, 0, ex.Message);
        }
    }

    // ---------------- URL construction ----------------

    private string BuildBoundingBoxUrl(DateTime startUtc, double minMag,
        double minLat, double maxLat, double minLon, double maxLon)
        => $"{_options.BaseUrl}?format=geojson" +
           $"&starttime={Iso(startUtc)}" +
           $"&minmagnitude={Num(minMag)}" +
           $"&minlatitude={Num(minLat)}&maxlatitude={Num(maxLat)}" +
           $"&minlongitude={Num(minLon)}&maxlongitude={Num(maxLon)}" +
           $"&orderby=time&limit={_options.MaxRecordsPerQuery}";

    // InvariantCulture is mandatory. On a bn-BD or any comma-decimal locale, "23.7" would
    // serialise as "23,7" and USGS would reject the request.
    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    // ---------------- Fetch + map ----------------

    private async Task<List<EarthquakeEvent>> FetchAsync(string url, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("usgs");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<FeatureCollectionDto>(stream, JsonOpts, ct);

        var results = new List<EarthquakeEvent>();
        if (payload?.Features is null) return results;

        var now = DateTime.UtcNow;

        foreach (var f in payload.Features)
        {
            var mapped = Map(f, now);
            if (mapped is not null) results.Add(mapped);
        }

        return results;
    }

    private static EarthquakeEvent? Map(FeatureDto f, DateTime fetchedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(f.Id)) return null;
        if (f.Properties is null || f.Geometry?.Coordinates is null) return null;

        // GeoJSON order is [longitude, latitude, depth] — NOT lat/lon.
        var c = f.Geometry.Coordinates;
        if (c.Count < 2) return null;

        double lon = c[0];
        double lat = c[1];
        double depth = c.Count > 2 ? c[2] : 0d;

        if (f.Properties.Time is null) return null;

        var occurred = DateTimeOffset.FromUnixTimeMilliseconds(f.Properties.Time.Value).UtcDateTime;
        var usgsUpdated = f.Properties.Updated is null
            ? (DateTime?)null
            : DateTimeOffset.FromUnixTimeMilliseconds(f.Properties.Updated.Value).UtcDateTime;

        var (divisionName, divisionKm) = SeismicGeography.NearestDivision(lat, lon);

        return new EarthquakeEvent
        {
            ExternalId = f.Id!,
            Source = "USGS",
            Magnitude = f.Properties.Mag,
            MagnitudeType = Truncate(f.Properties.MagType, 16),
            Place = Truncate(f.Properties.Place, 256),
            Latitude = lat,
            Longitude = lon,
            DepthKm = depth,
            OccurredAtUtc = occurred,
            UsgsUpdatedAtUtc = usgsUpdated,
            FetchedAtUtc = fetchedAtUtc,
            Scope = SeismicGeography.Classify(lat, lon),
            DistanceFromDhakaKm = Math.Round(SeismicGeography.DistanceFromDhakaKm(lat, lon), 1),
            NearestDivision = divisionName,
            NearestDivisionKm = Math.Round(divisionKm, 1),
            TsunamiFlag = f.Properties.Tsunami == 1,
            FeltReports = f.Properties.Felt,
            ReviewStatus = Truncate(f.Properties.Status, 32),
            DetailsUrl = Truncate(f.Properties.Url, 512)
        };
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    // ---------------- Upsert ----------------

    private async Task<(int Inserted, int Updated)> UpsertAsync(List<EarthquakeEvent> incoming, CancellationToken ct)
    {
        if (incoming.Count == 0) return (0, 0);

        var existing = new Dictionary<string, EarthquakeEvent>(StringComparer.Ordinal);

        // SQL Server caps a single command at 2100 parameters, so a naive
        // Contains(list-of-2000-ids) will throw. Chunk the lookup.
        foreach (var chunk in incoming.Select(e => e.ExternalId).Chunk(500))
        {
            var ids = chunk.ToArray();
            var rows = await _db.EarthquakeEvents
                .Where(e => ids.Contains(e.ExternalId))
                .ToListAsync(ct);

            foreach (var row in rows) existing[row.ExternalId] = row;
        }

        int inserted = 0, updated = 0;

        foreach (var item in incoming)
        {
            if (!existing.TryGetValue(item.ExternalId, out var row))
            {
                _db.EarthquakeEvents.Add(item);
                inserted++;
                continue;
            }

            // Only write if USGS actually revised the event. Magnitudes and locations do
            // get corrected hours after first publication, so we cannot treat records
            // as immutable — but nor should we dirty every row on every sync.
            bool revised = item.UsgsUpdatedAtUtc.HasValue
                           && (!row.UsgsUpdatedAtUtc.HasValue || item.UsgsUpdatedAtUtc > row.UsgsUpdatedAtUtc);

            row.FetchedAtUtc = item.FetchedAtUtc;

            // Derived geography is recomputed on EVERY sync, not only on a USGS
            // revision. It depends on OUR classification rules, which change when the
            // bounding boxes or felt radius are tuned — and a row written under the old
            // rules would otherwise keep a stale Scope forever.
            row.Scope = item.Scope;
            row.DistanceFromDhakaKm = item.DistanceFromDhakaKm;
            row.NearestDivision = item.NearestDivision;
            row.NearestDivisionKm = item.NearestDivisionKm;

            if (!revised) continue;

            row.Magnitude = item.Magnitude;
            row.MagnitudeType = item.MagnitudeType;
            row.Place = item.Place;
            row.Latitude = item.Latitude;
            row.Longitude = item.Longitude;
            row.DepthKm = item.DepthKm;
            row.OccurredAtUtc = item.OccurredAtUtc;
            row.UsgsUpdatedAtUtc = item.UsgsUpdatedAtUtc;
            row.Scope = item.Scope;
            row.DistanceFromDhakaKm = item.DistanceFromDhakaKm;
            row.NearestDivision = item.NearestDivision;
            row.NearestDivisionKm = item.NearestDivisionKm;
            row.TsunamiFlag = item.TsunamiFlag;
            row.FeltReports = item.FeltReports;
            row.ReviewStatus = item.ReviewStatus;
            row.DetailsUrl = item.DetailsUrl;

            updated++;
        }

        await _db.SaveChangesAsync(ct);
        return (inserted, updated);
    }

    // ---------------- GeoJSON DTOs ----------------

    private sealed class FeatureCollectionDto
    {
        public List<FeatureDto>? Features { get; set; }
    }

    private sealed class FeatureDto
    {
        public string? Id { get; set; }
        public PropertiesDto? Properties { get; set; }
        public GeometryDto? Geometry { get; set; }
    }

    private sealed class PropertiesDto
    {
        public double? Mag { get; set; }
        public string? Place { get; set; }
        public long? Time { get; set; }
        public long? Updated { get; set; }
        public string? Url { get; set; }
        public int? Felt { get; set; }
        public int? Tsunami { get; set; }
        public string? Status { get; set; }
        public string? MagType { get; set; }
    }

    private sealed class GeometryDto
    {
        public List<double>? Coordinates { get; set; }
    }
}