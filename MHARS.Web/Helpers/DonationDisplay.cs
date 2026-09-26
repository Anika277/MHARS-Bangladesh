using System.Globalization;

namespace MHARS.Web.Helpers;

/// <summary>Formatting helpers for money and Bangladesh time on donation pages.</summary>
public static class DonationDisplay
{
    // en-IN gives South-Asian lakh grouping: 1,25,000 — the way amounts are read in Bangladesh.
    private static readonly CultureInfo LakhCulture = TryCulture("en-IN");

    public static string Taka(decimal amount, bool withDecimals = false) =>
        "৳" + amount.ToString(withDecimals ? "N2" : "N0", LakhCulture);

    /// <summary>UTC → Bangladesh Standard Time (UTC+6, no daylight saving).</summary>
    public static DateTime ToBdTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(6);

    public static string BdTime(DateTime utc) =>
        ToBdTime(utc).ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture) + " (BST)";

    private static CultureInfo TryCulture(string name)
    {
        try { return CultureInfo.GetCultureInfo(name); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
