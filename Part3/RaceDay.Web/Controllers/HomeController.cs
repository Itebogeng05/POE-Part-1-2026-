using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly IRaceDayApi _api;

    public HomeController(IRaceDayApi api) => _api = api;

    public async Task<IActionResult> Index([FromQuery] EventFilter filter)
    {
        var user = CurrentUser;

        // Visitors see the welcome page. Organisers go to their dashboard. Participants see upcoming events.
        if (user is null || user.IsExpired)
        {
            if (user is not null) HttpContext.Session.SignOut();
            var teaser = await _api.GetEventsAsync(upcomingOnly: true);
            ViewData["Teaser"] = teaser.Success ? teaser.Data!.Take(3).ToList() : new List<EventDto>();
            return View("Landing");
        }
        if (user.IsOrganiser) return RedirectToAction("Index", "Organiser");

        // A bad filter value (for example text in a number box) is ignored rather than crashing the page.
        if (!ModelState.IsValid)
        {
            ViewData["FilterNote"] = "Some of your filters weren't understood and were ignored.";
            ModelState.Clear();
        }

        var events = await _api.GetEventsAsync(upcomingOnly: true);
        if (Expired(events) is { } redirect) return redirect;

        var model = new EventListViewModel { Filter = filter, UserName = user.FullName };
        if (events.Success)
        {
            model.TotalUpcoming = events.Data!.Count;
            model.Events = Apply(events.Data!, filter);
        }
        else
        {
            ViewData["LoadError"] = events.Error;
        }
        return View(model);
    }

    private static List<EventDto> Apply(IEnumerable<EventDto> source, EventFilter f)
    {
        var query = source;

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var text = f.Q.Trim();
            query = query.Where(e =>
                e.EventName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (e.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(f.Type))
            query = query.Where(e => string.Equals(e.EventType, f.Type.Trim(), StringComparison.OrdinalIgnoreCase));

        if (f.From.HasValue)
        {
            var from = DateOnly.FromDateTime(f.From.Value);
            query = query.Where(e => e.EventDate >= from);
        }
        if (f.To.HasValue)
        {
            var to = DateOnly.FromDateTime(f.To.Value);
            query = query.Where(e => e.EventDate <= to);
        }

        // Distance filter matches the event's headline distance or any of its categories.
        if (f.MinKm.HasValue || f.MaxKm.HasValue)
        {
            query = query.Where(e => e.Categories.Select(c => c.DistanceKm).Append(e.DistanceKm)
                .Any(d => (f.MinKm is null || d >= f.MinKm) && (f.MaxKm is null || d <= f.MaxKm)));
        }

        return query.OrderBy(e => e.EventDate).ToList();
    }

    /// <summary>Shown when something unexpected goes wrong. Never shows exception details to the user.</summary>
    public IActionResult Error() => View();

    /// <summary>Friendly pages for 404, 403 and other status codes.</summary>
    public IActionResult Status(int code = 500)
    {
        ViewData["Code"] = code;
        return View(code);
    }
}
