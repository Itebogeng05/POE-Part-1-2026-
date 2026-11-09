using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Base for every Organiser page: only a logged-in Organiser gets past the session check.</summary>
[SessionAuthorize(RoleNames.Organiser)]
public abstract class OrganiserBaseController : AppController
{
    protected readonly IRaceDayApi Api;

    protected OrganiserBaseController(IRaceDayApi api) => Api = api;

    /// <summary>The events created by the logged-in Organiser, soonest first.</summary>
    protected async Task<ApiResult<List<EventDto>>> GetMyEventsAsync()
    {
        var all = await Api.GetEventsAsync();
        if (!all.Success) return all;

        var userId = CurrentUser!.UserId;
        return ApiResult<List<EventDto>>.Ok(all.Data!.Where(e => e.OrganiserId == userId).OrderBy(e => e.EventDate).ToList());
    }

    /// <summary>
    /// Loads one event and checks it belongs to this Organiser. Returns either the event or the page to send the user to
    /// (login when the session expired, 404 when missing, access denied when it is someone else's event).
    /// </summary>
    protected async Task<(EventDto? Event, IActionResult? Redirect)> LoadOwnedEventAsync(int eventId)
    {
        var ev = await Api.GetEventAsync(eventId);
        if (Expired(ev) is { } expired) return (null, expired);
        if (ev.IsNotFound) return (null, NotFoundPage());
        if (!ev.Success)
        {
            FlashError(ev.Error ?? "We couldn't load that event.");
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }
        if (ev.Data!.OrganiserId != CurrentUser!.UserId)
            return (null, RedirectToAction("AccessDenied", "Account"));

        return (ev.Data, null);
    }
}

/// <summary>Checks a chosen banner image before it is sent to the API (the API checks it again).</summary>
public static class BannerRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    public static string? Validate(IFormFile? file, out string contentType)
    {
        contentType = string.Empty;
        if (file is null || file.Length == 0) return null; // The banner is optional.

        if (file.Length > MaxBytes) return "The banner must be 5 MB or smaller.";
        if (!Types.TryGetValue(Path.GetExtension(file.FileName), out var type))
            return "The banner must be a JPG, PNG, WEBP or GIF image.";

        contentType = type;
        return null;
    }
}
