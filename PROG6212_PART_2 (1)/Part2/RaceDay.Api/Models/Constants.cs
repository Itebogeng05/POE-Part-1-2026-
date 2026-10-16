namespace RaceDay.Api.Models;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class EventTypes
{
    public static readonly string[] All = { "Run", "Walk", "Cycle" };

    public static bool TryNormalise(string? value, out string normalised)
    {
        var match = All.FirstOrDefault(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        normalised = match ?? string.Empty;
        return match is not null;
    }
}
