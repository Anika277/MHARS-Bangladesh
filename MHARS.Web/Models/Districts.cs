namespace MHARS.Web.Models;

public static class Districts
{
    public static readonly string[] List =
    [
        "Dhaka", "Chattogram", "Khulna", "Rajshahi", "Sylhet", "Barishal",
        "Rangpur", "Mymensingh", "Cumilla", "Cox's Bazar", "Jamalpur",
        "Sirajganj", "Gaibandha", "Kurigram", "Bogra", "Faridpur",
        "Patuakhali", "Bhola", "Sunamganj", "Habiganj"
    ];

    // District HQ approximate coordinates for the map view (demo design)
    public static readonly Dictionary<string, (double Lat, double Lng)> Coordinates = new()
    {
        ["Dhaka"] = (23.8103, 90.4125),
        ["Chattogram"] = (22.3569, 91.7832),
        ["Khulna"] = (22.8456, 89.5403),
        ["Rajshahi"] = (24.3745, 88.6042),
        ["Sylhet"] = (24.8949, 91.8687),
        ["Barishal"] = (22.7010, 90.3535),
        ["Rangpur"] = (25.7439, 89.2752),
        ["Mymensingh"] = (24.7471, 90.4203),
        ["Cumilla"] = (23.4607, 91.1809),
        ["Cox's Bazar"] = (21.4272, 92.0058),
        ["Jamalpur"] = (24.9375, 89.9372),
        ["Sirajganj"] = (24.4534, 89.7008),
        ["Gaibandha"] = (25.3288, 89.5281),
        ["Kurigram"] = (25.8073, 89.6367),
        ["Bogra"] = (24.8481, 89.3730),
        ["Faridpur"] = (23.6070, 89.8429),
        ["Patuakhali"] = (22.3596, 90.3299),
        ["Bhola"] = (22.6859, 90.6482),
        ["Sunamganj"] = (25.0658, 91.4073),
        ["Habiganj"] = (24.3745, 91.4155)
    };
}
