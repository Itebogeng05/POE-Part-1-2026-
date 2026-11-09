using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Events")]
public class EventsController : AppController
{
    private readonly IRaceDayApi _api;

    public EventsController(IRaceDayApi api) => _api = api;

    /// <summary>Full event information, its categories, the banner and (for a participant) the enrol form.</summary>
    [HttpGet("{id:int}")]
    [SessionAuthorize]
    public async Task<IActionResult> Details(int id)
    {
        var user = CurrentUser!;
        var ev = await _api.GetEventAsync(id);
        if (Expired(ev) is { } redirect) return redirect;
        if (ev.IsNotFound) return NotFoundPage();
        if (!ev.Success)
        {
            FlashError(ev.Error ?? "We couldn't load that event.");
            return RedirectToAction("Index", "Home");
        }

        var model = new EventDetailsViewModel { Event = ev.Data!, IsOwner = user.IsOrganiser && ev.Data!.OrganiserId == user.UserId };

        if (user.IsParticipant)
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            if (Expired(mine) is { } expired) return expired;
            if (mine.Success) model.MyEnrolment = mine.Data!.FirstOrDefault(e => e.EventId == id);
        }

        model.CanEnrol = user.IsParticipant && model.Event.IsUpcoming && model.MyEnrolment is null && model.Event.Categories.Count > 0;

        // Once the event is over, show the published results.
        if (!model.Event.IsUpcoming)
        {
            var results = await _api.GetEventResultsAsync(id);
            if (results.Success) model.Results = results.Data!.Where(r => r.Position.HasValue).ToList();
        }

        return View(model);
    }

    /// <summary>
    /// The banner image. The browser asks this app, this app asks the API, and the API reads Azure Blob Storage.
    /// The MVC project never talks to Azure directly.
    /// </summary>
    [HttpGet("{id:int}/Banner")]
    public async Task<IActionResult> Banner(int id)
    {
        var image = await _api.GetBannerAsync(id);
        if (!image.Success || image.Data is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=300";
        return File(image.Data.Bytes, image.Data.ContentType);
    }
}
