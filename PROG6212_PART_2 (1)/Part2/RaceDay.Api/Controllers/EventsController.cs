using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>List events (public).</summary>
    /// <remarks>Expects no body. Add ?upcomingOnly=true to hide events that have already taken place.</remarks>
    /// <response code="200">Returns the events, earliest first, each with its categories.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool upcomingOnly = false)
    {
        var query = EventsReadOnly();
        if (upcomingOnly)
        {
            var today = Today;
            query = query.Where(e => e.EventDate >= today);
        }
        return Ok(await query.OrderBy(e => e.EventDate).Select(Mappings.EventToResponse).ToListAsync());
    }

    /// <summary>Get one event and its categories (public).</summary>
    /// <response code="200">Returns the event with nested categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await EventsReadOnly().Where(e => e.EventId == id).Select(Mappings.EventToResponse).FirstOrDefaultAsync();
        return ev is null ? EventNotFound() : Ok(ev);
    }

    /// <summary>Create an event (Organiser only). The logged-in Organiser becomes the owner.</summary>
    /// <remarks>
    /// Expects: eventName, eventDate (yyyy-MM-dd, not in the past), location, optional description,
    /// eventType (Run, Walk or Cycle) and distanceKm.
    /// </remarks>
    /// <response code="201">Event created. Returns the event.</response>
    /// <response code="400">A field is invalid, the date is in the past, or the event type is not Run/Walk/Cycle.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var invalid = Validate(request, out var eventType);
        if (invalid is not null) return invalid;

        var ev = new Event
        {
            OrganiserId = User.GetUserId(),
            EventName = request.EventName.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            Description = request.Description?.Trim(),
            EventType = eventType,
            DistanceKm = request.DistanceKm
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, Mappings.EventToResponseFunc(ev));
    }

    /// <summary>Update an event you own (Organiser only).</summary>
    /// <remarks>Expects the same fields as creating an event.</remarks>
    /// <response code="200">Returns the updated event.</response>
    /// <response code="400">A field is invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return EventNotFound();
        if (ev.OrganiserId != User.GetUserId()) return NotOwner();

        var invalid = Validate(request, out var eventType);
        if (invalid is not null) return invalid;

        ev.EventName = request.EventName.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventType = eventType;
        ev.DistanceKm = request.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(await EventsReadOnly().Where(e => e.EventId == id).Select(Mappings.EventToResponse).FirstAsync());
    }

    /// <summary>Delete an event you own (Organiser only). Its categories, enrolments and results are removed too.</summary>
    /// <response code="200">Event deleted.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return EventNotFound();
        if (ev.OrganiserId != User.GetUserId()) return NotOwner();

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Event deleted." });
    }

    /// <summary>List the categories of an event (public).</summary>
    /// <response code="200">Returns the categories, shortest distance first.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id)) return EventNotFound();

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderBy(c => c.DistanceKm)
            .Select(Mappings.CategoryToResponse)
            .ToListAsync();
        return Ok(categories);
    }

    /// <summary>Add a category (for example 10km or Under 20) to an event you own (Organiser only).</summary>
    /// <remarks>Expects: categoryName, distanceKm and entryFee.</remarks>
    /// <response code="201">Category created. Returns the category.</response>
    /// <response code="400">A field is invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPost("{id:int}/categories")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddCategory(int id, CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return EventNotFound();
        if (ev.OrganiserId != User.GetUserId()) return NotOwner();

        var category = new Category
        {
            EventId = id,
            CategoryName = request.CategoryName.Trim(),
            DistanceKm = request.DistanceKm,
            EntryFee = request.EntryFee
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{id}/categories", Mappings.CategoryToResponseFunc(category));
    }

    /// <summary>List everyone enrolled in an event you own (Organiser only).</summary>
    /// <response code="200">Returns the enrolments with participant details.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolments(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return EventNotFound();
        if (ev.OrganiserId != User.GetUserId()) return NotOwner();

        var enrolments = await _db.Enrolments.AsNoTracking()
            .Where(en => en.Category.EventId == id)
            .OrderBy(en => en.EnrolmentDate)
            .Select(Mappings.EnrolmentToResponse)
            .ToListAsync();
        return Ok(enrolments);
    }

    /// <summary>The results list (leaderboard) for an event (public).</summary>
    /// <response code="200">Returns the results ordered by finishing position.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}/results")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id)) return EventNotFound();

        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == id)
            .OrderBy(r => r.Position == null).ThenBy(r => r.Position)
            .Select(Mappings.ResultToResponse)
            .ToListAsync();
        return Ok(results);
    }

    // ---------- helpers ----------
    private IQueryable<Event> EventsReadOnly() => _db.Events.AsNoTracking();

    private IActionResult? Validate(EventRequest request, out string eventType)
    {
        if (!EventTypes.TryNormalise(request.EventType, out eventType))
            ModelState.AddModelError(nameof(request.EventType), "Event type must be Run, Walk or Cycle.");
        if (request.EventDate is { } date && date < Today)
            ModelState.AddModelError(nameof(request.EventDate), "The event date cannot be in the past.");

        return ModelState.IsValid ? null : ValidationProblem(ModelState);
    }

    private IActionResult EventNotFound() => Problem(title: "Event not found.", statusCode: StatusCodes.Status404NotFound);
    private IActionResult NotOwner() => Problem(title: "You can only manage your own events.", statusCode: StatusCodes.Status403Forbidden);
}
