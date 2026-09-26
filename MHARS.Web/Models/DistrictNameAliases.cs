namespace MHARS.Web.Models;

/// <summary>
/// Other ways each district's name is written on official pages: old English spellings
/// and the Bangla name. Used by the alert verifier to check that an official source page
/// really talks about the alert's district (FFWC/BMD pages are often in Bangla).
/// Keys match Districts.List exactly.
/// </summary>
public static class DistrictNameAliases
{
    public static readonly Dictionary<string, string[]> Map = new()
    {
        ["Dhaka"]       = ["Dhaka", "ঢাকা"],
        ["Chattogram"]  = ["Chattogram", "Chittagong", "চট্টগ্রাম"],
        ["Khulna"]      = ["Khulna", "খুলনা"],
        ["Rajshahi"]    = ["Rajshahi", "রাজশাহী"],
        ["Sylhet"]      = ["Sylhet", "সিলেট"],
        ["Barishal"]    = ["Barishal", "Barisal", "বরিশাল"],
        ["Rangpur"]     = ["Rangpur", "রংপুর"],
        ["Mymensingh"]  = ["Mymensingh", "ময়মনসিংহ"],
        ["Cumilla"]     = ["Cumilla", "Comilla", "কুমিল্লা"],
        ["Cox's Bazar"] = ["Cox's Bazar", "Cox’s Bazar", "Coxs Bazar", "Coxsbazar", "কক্সবাজার"],
        ["Jamalpur"]    = ["Jamalpur", "জামালপুর"],
        ["Sirajganj"]   = ["Sirajganj", "Sirajgonj", "সিরাজগঞ্জ"],
        ["Gaibandha"]   = ["Gaibandha", "গাইবান্ধা"],
        ["Kurigram"]    = ["Kurigram", "কুড়িগ্রাম"],
        ["Bogra"]       = ["Bogra", "Bogura", "বগুড়া"],
        ["Faridpur"]    = ["Faridpur", "ফরিদপুর"],
        ["Patuakhali"]  = ["Patuakhali", "পটুয়াখালী"],
        ["Bhola"]       = ["Bhola", "ভোলা"],
        ["Sunamganj"]   = ["Sunamganj", "Sunamgonj", "সুনামগঞ্জ"],
        ["Habiganj"]    = ["Habiganj", "Habigonj", "হবিগঞ্জ"],
    };

    /// <summary>All names to look for; falls back to the district itself if it is not in the map.</summary>
    public static string[] For(string? district) =>
        string.IsNullOrWhiteSpace(district) ? []
        : Map.TryGetValue(district, out var names) ? names
        : [district];
}
