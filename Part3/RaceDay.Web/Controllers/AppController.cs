using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for every controller: current user, flash messages and API error handling.</summary>
public abstract class AppController : Controller
{
    protected SessionUser? CurrentUser => HttpContext.Session.GetUser();

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>
    /// When the API says 401 for a logged-in user, their token has expired: clear the session and send them to Login.
    /// Returns null when the result is not a 401, so callers can write <c>if (Expired(r) is { } redirect) return redirect;</c>.
    /// </summary>
    protected IActionResult? Expired(ApiResult result)
    {
        if (!result.IsUnauthorized) return null;
        HttpContext.Session.SignOut();
        FlashError("Your session has expired. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Copies API validation problems onto the form: field errors beside the field, everything else in the summary.</summary>
    protected void AddApiErrors<TModel>(ApiResult result)
    {
        var properties = typeof(TModel).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (field, messages) in result.FieldErrors)
        {
            var match = properties.FirstOrDefault(p => string.Equals(p, field, StringComparison.OrdinalIgnoreCase));
            foreach (var message in messages)
                ModelState.AddModelError(match ?? string.Empty, message);
        }

        if (result.FieldErrors.Count == 0 && !string.IsNullOrWhiteSpace(result.Error))
            ModelState.AddModelError(string.Empty, result.Error);
    }

    protected static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    protected IActionResult NotFoundPage() => NotFound();
}
