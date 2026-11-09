using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Participant")]
[SessionAuthorize(RoleNames.Participant)]
public class ParticipantController : AppController
{
    private readonly IRaceDayApi _api;

    public ParticipantController(IRaceDayApi api) => _api = api;

    /// <summary>Enrol for an event by choosing a category. The API records the enrolment immediately.</summary>
    [HttpPost("Enrol"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null or <= 0)
        {
            FlashError("Please choose a category before you enrol.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        var result = await _api.EnrolAsync(categoryId.Value);
        if (Expired(result) is { } redirect) return redirect;

        if (!result.Success)
        {
            FlashError(result.Error ?? "We couldn't complete your enrolment. Please try again.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        FlashSuccess($"You're in! You are enrolled in {result.Data!.EventName} ({result.Data.CategoryName}).");
        return RedirectToAction(nameof(Enrolments));
    }

    [HttpGet("Enrolments")]
    public async Task<IActionResult> Enrolments()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (Expired(result) is { } redirect) return redirect;

        if (!result.Success) ViewData["LoadError"] = result.Error;
        return View(result.Success ? result.Data! : new List<EnrolmentDto>());
    }

    [HttpPost("Enrolments/{id:int}/Cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.CancelEnrolmentAsync(id);
        if (Expired(result) is { } redirect) return redirect;

        if (result.Success) FlashSuccess("Your enrolment has been cancelled.");
        else FlashError(result.Error ?? "We couldn't cancel that enrolment.");
        return RedirectToAction(nameof(Enrolments));
    }

    /// <summary>Personal race history: finish time, and position out of everyone who finished that event.</summary>
    [HttpGet("Results")]
    public async Task<IActionResult> Results()
    {
        var mine = await _api.GetMyResultsAsync();
        if (Expired(mine) is { } redirect) return redirect;

        if (!mine.Success)
        {
            ViewData["LoadError"] = mine.Error;
            return View(new List<MyResultRow>());
        }

        // Event dates come from the participant's enrolments; "out of N" comes from each event's full results list.
        var enrolments = await _api.GetMyEnrolmentsAsync();
        var dates = enrolments.Success
            ? enrolments.Data!.GroupBy(e => e.EnrolmentId).ToDictionary(g => g.Key, g => g.First().EventDate)
            : new Dictionary<int, DateOnly>();

        var eventIds = mine.Data!.Select(r => r.EventId).Distinct().ToList();
        var finisherCalls = await Task.WhenAll(eventIds.Select(async id => (id, await _api.GetEventResultsAsync(id))));
        var finishers = finisherCalls.ToDictionary(
            c => c.id,
            c => c.Item2.Success ? (int?)c.Item2.Data!.Count(r => r.Position.HasValue) : null);

        var rows = mine.Data!
            .Select(r => new MyResultRow
            {
                Result = r,
                EventDate = dates.TryGetValue(r.EnrolmentId, out var d) ? d : null,
                TotalFinishers = finishers.GetValueOrDefault(r.EventId)
            })
            .OrderByDescending(r => r.EventDate ?? DateOnly.MinValue)
            .ToList();

        return View(rows);
    }
}
