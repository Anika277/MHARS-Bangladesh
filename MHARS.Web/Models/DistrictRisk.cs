namespace MHARS.Web.Models;

public class DistrictRisk
{
    public string District { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }

    public int FloodScore { get; set; }
    public string FloodCategory { get; set; } = string.Empty;

    public int EarthquakeScore { get; set; }
    public string EarthquakeCategory { get; set; } = string.Empty;

    public int OverallScore { get; set; }
    public string OverallCategory { get; set; } = string.Empty;
}