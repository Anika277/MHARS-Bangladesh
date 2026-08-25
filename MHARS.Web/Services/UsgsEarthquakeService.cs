using System.Text.Json;

namespace MHARS.Web.Services;

public class EarthquakeEvent
{
    public string Place { get; set; } = string.Empty;
    public double Magnitude { get; set; }
    public double DepthKm { get; set; }
    public DateTime TimeUtc { get; set; }
    public string Url { get; set; } = string.Empty;
    public string SeverityLabel => Magnitude switch
    {
        < 4.0 => "Minor",
        <= 6.0 => "Moderate",
        _ => "Severe"
    };
    public string BadgeClass => Magnitude switch
    {
        < 4.0 => "success",
        <= 6.0 => "warning",
        _ => "danger"
    };
}

public interface IUsgsEarthquakeService
{
    Task<List<EarthquakeEvent>> GetRecentEarthquakesAsync();
}

public class UsgsEarthquakeService(HttpClient httpClient) : IUsgsEarthquakeService
{
    private const string FeedUrl =
        "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_day.geojson";

    public async Task<List<EarthquakeEvent>> GetRecentEarthquakesAsync()
    {
        try
        {
            using var stream = await httpClient.GetStreamAsync(FeedUrl);
            using var doc = await JsonDocument.ParseAsync(stream);

            return doc.RootElement.GetProperty("features").EnumerateArray()
                .Where(f =>
                {
                    var props = f.GetProperty("properties");
                    return props.TryGetProperty("mag", out var mag) && mag.ValueKind == JsonValueKind.Number;
                })
                .Select(f =>
                {
                    var props = f.GetProperty("properties");
                    var geo = f.GetProperty("geometry").GetProperty("coordinates");
                    return new EarthquakeEvent
                    {
                        Place = props.GetProperty("place").GetString() ?? "Unknown location",
                        Magnitude = Math.Round(props.GetProperty("mag").GetDouble(), 1),
                        DepthKm = Math.Round(geo[2].GetDouble(), 1),
                        TimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(props.GetProperty("time").GetInt64()).UtcDateTime,
                        Url = props.GetProperty("url").GetString() ?? "#"
                    };
                })
                .OrderByDescending(e => e.TimeUtc)
                .Take(25)
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
