using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Organiser/Events")]
public class OrganiserEventsController : OrganiserBaseController
{
    public OrganiserEventsController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var events = await GetMyEventsAsync();
        if (Expired(events) is { } redirect) return redirect;

        if (!events.Success) ViewData["LoadError"] = events.Error;
        return View(events.Success ? events.Data! : new List<EventDto>());
    }

    [HttpGet("Create")]
    public IActionResult Create() =>
        View("Form", new EventFormViewModel { EventDate = DateTime.UtcNow.Date.AddDays(30) });

    [HttpPost("Create"), ValidateAntiForgeryToken]
    [RequestSizeLimit(BannerRules.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        var bannerProblem = BannerRules.Validate(model.Banner, out var bannerType);
        if (bannerProblem is not null) ModelState.AddModelError(nameof(model.Banner), bannerProblem);
        if (!ModelState.IsValid) return View("Form", model);

        var created = await Api.CreateEventAsync(ToRequest(model));
        if (Expired(created) is { } redirect) return redirect;
        if (!created.Success)
        {
            AddApiErrors<EventFormViewModel>(created);
            return View("Form", model);
        }

        // The image goes to the API, which stores it in Azure Blob Storage.
        if (model.Banner is { Length: > 0 })
        {
            var upload = await UploadBannerAsync(created.Data!.EventId, model.Banner, bannerType);
            if (!upload.Success)
            {
                FlashError($"\"{created.Data.EventName}\" was created, but the banner could not be uploaded: {upload.Error} You can add it by editing the event.");
                return RedirectToAction(nameof(Index));
            }
        }

        FlashSuccess($"\"{created.Data!.EventName}\" has been created. Add some categories so people can enrol.");
        return RedirectToAction("Index", "OrganiserCategories", new { eventId = created.Data.EventId });
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, redirect) = await LoadOwnedEventAsync(id);
        if (redirect is not null) return redirect;

        return View("Form", new EventFormViewModel
        {
            EventId = ev!.EventId,
            EventName = ev.EventName,
            Description = ev.Description,
            EventDate = ev.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = ev.Location,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            HasBanner = ev.HasBanner
        });
    }

    [HttpPost("{id:int}/Edit"), ValidateAntiForgeryToken]
    [RequestSizeLimit(BannerRules.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;

        var bannerProblem = BannerRules.Validate(model.Banner, out var bannerType);
        if (bannerProblem is not null) ModelState.AddModelError(nameof(model.Banner), bannerProblem);
        if (!ModelState.IsValid) return View("Form", model);

        var updated = await Api.UpdateEventAsync(id, ToRequest(model));
        if (Expired(updated) is { } redirect) return redirect;
        if (updated.IsNotFound) return NotFoundPage();
        if (updated.IsForbidden) return RedirectToAction("AccessDenied", "Account");
        if (!updated.Success)
        {
            AddApiErrors<EventFormViewModel>(updated);
            return View("Form", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var upload = await UploadBannerAsync(id, model.Banner, bannerType);
            if (!upload.Success)
            {
                FlashError($"The event was saved, but the new banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(Index));
            }
        }

        FlashSuccess($"\"{updated.Data!.EventName}\" has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        if (Expired(result) is { } redirect) return redirect;

        if (result.Success) FlashSuccess("The event, its categories and enrolments have been deleted.");
        else FlashError(result.Error ?? "We couldn't delete that event.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file, string contentType)
    {
        await using var stream = file.OpenReadStream();
        return await Api.UploadBannerAsync(eventId, stream, Path.GetFileName(file.FileName), contentType);
    }

    private static object ToRequest(EventFormViewModel m) => new
    {
        eventName = m.EventName.Trim(),
        eventDate = DateOnly.FromDateTime(m.EventDate!.Value),
        location = m.Location.Trim(),
        description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim(),
        eventType = m.EventType,
        distanceKm = m.DistanceKm!.Value
    };
}
