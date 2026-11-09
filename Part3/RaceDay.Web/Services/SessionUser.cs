namespace RaceDay.Web.Services;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>What the MVC app remembers about the logged-in user for the whole session.</summary>
public sealed record SessionUser(int UserId, string Role, string FullName, string Token, DateTime ExpiresAtUtc)
{
    public bool IsOrganiser => Role == RoleNames.Organiser;
    public bool IsParticipant => Role == RoleNames.Participant;
    public bool IsExpired => ExpiresAtUtc <= DateTime.UtcNow;
}

public static class UserSessionExtensions
{
    private const string UserIdKey = "UserId";
    private const string RoleKey = "Role";
    private const string NameKey = "FullName";
    private const string TokenKey = "Token";
    private const string ExpiryKey = "TokenExpiresUtc";

    public static void SignIn(this ISession session, SessionUser user)
    {
        session.Clear();
        session.SetInt32(UserIdKey, user.UserId);
        session.SetString(RoleKey, user.Role);
        session.SetString(NameKey, user.FullName);
        session.SetString(TokenKey, user.Token);
        session.SetString(ExpiryKey, user.ExpiresAtUtc.ToString("O"));
    }

    public static SessionUser? GetUser(this ISession session)
    {
        var id = session.GetInt32(UserIdKey);
        var role = session.GetString(RoleKey);
        var token = session.GetString(TokenKey);
        if (id is null || string.IsNullOrEmpty(role) || string.IsNullOrEmpty(token)) return null;

        var expiry = DateTime.TryParse(session.GetString(ExpiryKey), null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTime.MinValue;

        return new SessionUser(id.Value, role, session.GetString(NameKey) ?? "", token, expiry);
    }

    public static void SignOut(this ISession session) => session.Clear();
}
