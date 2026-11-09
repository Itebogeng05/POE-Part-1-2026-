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
    private const long MaxBannerBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedBannerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<EventsController> _logger;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobs, ILogger<EventsController> logger)
    {
        _db = db;
        _blobs = blobs;
        _logger = logger;
    }

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

        var bannerBlob = ev.BannerBlobName;
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        await TryDeleteBlobAsync(bannerBlob);
        return Ok(new { message = "Event deleted." });
    }

    /// <summary>Upload or replace the banner image of an event you own (Organiser only).</summary>
    /// <remarks>
    /// Send multipart/form-data with one file field called "file" (jpg, png, webp or gif, max 5 MB).
    /// The image is stored in Azure Blob Storage; the previous banner, if any, is removed.
    /// </remarks>
    /// <response code="200">Banner stored. Returns the updated event.</response>
    /// <response code="400">No file, wrong type, too large, or not a real image.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own this event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBannerBytes + 512 * 1024)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile? file, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev is null) return EventNotFound();
        if (ev.OrganiserId != User.GetUserId()) return NotOwner();

        if (file is null || file.Length == 0)
            return Problem(title: "Please choose an image to upload.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxBannerBytes)
            return Problem(title: "The image must be 5 MB or smaller.", statusCode: StatusCodes.Status400BadRequest);

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedBannerTypes.TryGetValue(extension ?? string.Empty, out var contentType))
            return Problem(title: "Only JPG, PNG, WEBP or GIF images are allowed.", statusCode: StatusCodes.Status400BadRequest);

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        if (!LooksLikeImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), contentType))
            return Problem(title: "That file is not a valid image.", statusCode: StatusCodes.Status400BadRequest);
        buffer.Position = 0;

        string newBlob;
        try
        {
            newBlob = await _blobs.UploadAsync(buffer, contentType, extension!.ToLowerInvariant(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banner upload failed for event {EventId}.", id);
            return Problem(title: "Image storage is unavailable right now. Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var oldBlob = ev.BannerBlobName;
        ev.BannerBlobName = newBlob;
        await _db.SaveChangesAsync(ct);
        await TryDeleteBlobAsync(oldBlob);

        return Ok(await EventsReadOnly().Where(e => e.EventId == id).Select(Mappings.EventToResponse).FirstAsync(ct));
    }

    /// <summary>Download the banner image of an event (public).</summary>
    /// <response code="200">Returns the image file.</response>
    /// <response code="404">The event does not exist or has no banner.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpGet("{id:int}/banner")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBanner(int id, CancellationToken ct)
    {
        var blobName = await _db.Events.AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => e.BannerBlobName)
            .FirstOrDefaultAsync(ct);
        if (blobName is null)
            return Problem(title: "This event has no banner image.", statusCode: StatusCodes.Status404NotFound);

        try
        {
            var file = await _blobs.DownloadAsync(blobName, ct);
            if (file is null)
                return Problem(title: "This event has no banner image.", statusCode: StatusCodes.Status404NotFound);

            Response.Headers.CacheControl = "public, max-age=300";
            return File(file.Content, file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banner download failed for event {EventId}.", id);
            return Problem(title: "Image storage is unavailable right now.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
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
    private async Task TryDeleteBlobAsync(string? blobName)
    {
        if (string.IsNullOrEmpty(blobName)) return;
        try { await _blobs.DeleteAsync(blobName); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob {BlobName}.", blobName); }
    }

    /// <summary>Checks the first bytes of the file so a renamed .exe cannot pass as an image.</summary>
    private static bool LooksLikeImage(ReadOnlySpan<byte> b, string contentType) => contentType switch
    {
        "image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        "image/png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        "image/gif" => b.Length > 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38,
        "image/webp" => b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
                        && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50,
        _ => false
    };

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
