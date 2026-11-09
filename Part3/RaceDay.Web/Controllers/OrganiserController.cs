using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Organiser")]
public class OrganiserController : OrganiserBaseController
{
    public OrganiserController(IRaceDayApi api) : base(api) { }

    /// <summary>Dashboard: all my events, enrolments per event and upcoming dates.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var events = await GetMyEventsAsync();
        if (Expired(events) is { } redirect) return redirect;

        var model = new DashboardViewModel { OrganiserName = CurrentUser!.FullName };
        if (!events.Success)
        {
            model.Warning = events.Error;
            return View(model);
        }

        // One enrolment count per event, fetched in parallel.
        var counts = await Task.WhenAll(events.Data!.Select(async e => (e, await Api.GetEventEnrolmentsAsync(e.EventId))));
        var unauthorised = counts.Select(c => c.Item2).FirstOrDefault(r => r.IsUnauthorized);
        if (unauthorised is not null && Expired(unauthorised) is { } expired) return expired;

        model.Events = counts.Select(c => new EventSummary { Event = c.e, Enrolments = c.Item2.Success ? c.Item2.Data!.Count : 0 }).ToList();
        if (counts.Any(c => !c.Item2.Success))
            model.Warning = "Some enrolment totals couldn't be loaded right now.";

        return View(model);
    }
}
