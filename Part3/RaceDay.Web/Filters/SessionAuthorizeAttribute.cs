using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a controller or action using the session. Not logged in (or token expired) goes to Login.
/// Logged in with the wrong role goes to Access Denied. Pass a role name to require that role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string? _role;

    public SessionAuthorizeAttribute(string? role = null) => _role = role;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var user = http.Session.GetUser();

        if (user is null || user.IsExpired)
        {
            var expired = user is not null;
            http.Session.SignOut();
            var returnUrl = http.Request.Method == HttpMethods.Get ? http.Request.Path + http.Request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl, expired });
            return;
        }

        if (_role is not null && !string.Equals(user.Role, _role, StringComparison.Ordinal))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return;
        }

        // Stop the browser keeping protected pages, so the Back button cannot show them after logout.
        http.Response.Headers.CacheControl = "no-store, no-cache";
        http.Response.Headers.Pragma = "no-cache";
    }
}
