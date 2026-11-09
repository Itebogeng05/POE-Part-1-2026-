using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Organiser/Enrolments")]
public class OrganiserEnrolmentsController : OrganiserBaseController
{
    public OrganiserEnrolmentsController(IRaceDayApi api) : base(api) { }

    /// <summary>Everyone enrolled in one of my events, with their category and enrolment status.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int? eventId)
    {
        var mine = await GetMyEventsAsync();
        if (Expired(mine) is { } redirect) return redirect;

        var model = new EnrolmentsPageViewModel { MyEvents = mine.Success ? mine.Data! : new List<EventDto>() };
        if (!mine.Success) ViewData["LoadError"] = mine.Error;

        if (eventId is not null)
        {
            var selected = model.MyEvents.FirstOrDefault(e => e.EventId == eventId);
            if (selected is null && mine.Success) return NotFoundPage();

            if (selected is not null)
            {
                var enrolments = await Api.GetEventEnrolmentsAsync(selected.EventId);
                if (Expired(enrolments) is { } expired) return expired;

                model.Selected = selected;
                if (enrolments.Success) model.Enrolments = enrolments.Data!.OrderBy(e => e.ParticipantName).ToList();
                else ViewData["LoadError"] = enrolments.Error;
            }
        }

        return View(model);
    }
}
