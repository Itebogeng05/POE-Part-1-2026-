using System.Globalization;

namespace RaceDay.Web.Models;

/// <summary>One place for how dates, money, times and status colours look, so every page is consistent.</summary>
public static class UiFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Date(DateOnly date) => date.ToString("ddd, d MMM yyyy", Inv);
    public static string ShortDate(DateOnly date) => date.ToString("d MMM yyyy", Inv);
    public static string DateTimeShort(DateTime utc) => utc.ToLocalTime().ToString("d MMM yyyy, HH:mm", Inv);
    public static string Km(decimal km) => km.ToString("0.##", Inv) + " km";

    public static string Rand(decimal amount) => amount == 0 ? "Free" : "R " + amount.ToString("N2", Inv);

    public static string Time(TimeOnly? time) => time is null ? "-" : time.Value.ToString("HH:mm:ss", Inv);

    /// <summary>1 becomes 1st, 2 becomes 2nd, 11 becomes 11th, 47 becomes 47th.</summary>
    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 13) return n + "th";
        return n + (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
    }

    /// <summary>Bootstrap-style colour name for an enrolment or result status. Green = confirmed, amber = pending.</summary>
    public static string StatusClass(string? status) => (status ?? string.Empty).ToLowerInvariant() switch
    {
        "confirmed" or "final" or "published" => "confirmed",
        "pending" => "pending",
        "cancelled" or "rejected" => "cancelled",
        _ => "neutral"
    };

    public static string TypeIcon(string? type) => (type ?? string.Empty).ToLowerInvariant() switch
    {
        "run" => "bi-lightning-charge-fill",
        "walk" => "bi-person-walking",
        "cycle" => "bi-bicycle",
        _ => "bi-flag-fill"
    };
}
