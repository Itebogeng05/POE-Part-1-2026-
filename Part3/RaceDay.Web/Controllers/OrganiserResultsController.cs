using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Organiser/Results")]
public class OrganiserResultsController : OrganiserBaseController
{
    public OrganiserResultsController(IRaceDayApi api) : base(api) { }

    /// <summary>Capture finish times and positions for the people enrolled in one of my events.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(int? eventId)
    {
        var mine = await GetMyEventsAsync();
        if (Expired(mine) is { } redirect) return redirect;

        var model = new ResultsPageViewModel { MyEvents = mine.Success ? mine.Data! : new List<EventDto>() };
        if (!mine.Success) ViewData["LoadError"] = mine.Error;

        if (eventId is not null)
        {
            var selected = model.MyEvents.FirstOrDefault(e => e.EventId == eventId);
            if (selected is null && mine.Success) return NotFoundPage();

            if (selected is not null)
            {
                var enrolmentsTask = Api.GetEventEnrolmentsAsync(selected.EventId);
                var resultsTask = Api.GetEventResultsAsync(selected.EventId);
                await Task.WhenAll(enrolmentsTask, resultsTask);
                var enrolments = enrolmentsTask.Result;
                var results = resultsTask.Result;
                if (Expired(enrolments) is { } expired) return expired;

                model.Selected = selected;
                model.EventHasFinished = !selected.IsUpcoming;

                if (!enrolments.Success || !results.Success)
                {
                    ViewData["LoadError"] = enrolments.Error ?? results.Error;
                }
                else
                {
                    var byEnrolment = results.Data!.GroupBy(r => r.EnrolmentId).ToDictionary(g => g.Key, g => g.First());
                    model.Rows = enrolments.Data!
                        .OrderBy(e => byEnrolment.TryGetValue(e.EnrolmentId, out var r) && r.Position.HasValue ? r.Position.Value : int.MaxValue)
                        .ThenBy(e => e.ParticipantName)
                        .Select(e => new ResultRow { Enrolment = e, Result = byEnrolment.GetValueOrDefault(e.EnrolmentId) })
                        .ToList();
                }
            }
        }

        return View(model);
    }

    /// <summary>Save (publish) one participant's result. Creates it the first time, corrects it afterwards.</summary>
    [HttpPost("Save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ResultFormViewModel model)
    {
        var back = RedirectToAction(nameof(Index), new { eventId = model.EventId });

        if (!ModelState.IsValid)
        {
            // Inline forms: show the first problem as a message on the results page.
            FlashError(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                       ?? "Please check the finish time and position.");
            return back;
        }

        if (!TimeOnly.TryParseExact(model.FinishTime.PadLeft(8, '0'), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish))
        {
            FlashError("Use the format hh:mm:ss for the finish time, for example 00:47:32.");
            return back;
        }

        var (ev, redirect) = await LoadOwnedEventAsync(model.EventId);
        if (redirect is not null) return redirect;
        if (ev!.IsUpcoming)
        {
            FlashError("Results can only be entered once the event has taken place.");
            return back;
        }

        var result = model.ResultId is { } resultId
            ? await Api.UpdateResultAsync(resultId, finish, model.Position!.Value)
            : await Api.CreateResultAsync(model.EnrolmentId, finish, model.Position!.Value);

        if (Expired(result) is { } expired) return expired;

        if (result.Success)
            FlashSuccess($"Result published for {result.Data!.ParticipantName}: {UiFormat.Ordinal(result.Data.Position ?? 0)}, {UiFormat.Time(result.Data.FinishTime)}.");
        else
            FlashError(result.Error ?? "We couldn't save that result.");

        return back;
    }
}
