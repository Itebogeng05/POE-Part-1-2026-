using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/enrolments")]
[Authorize(Roles = RoleNames.Participant)]
[Produces("application/json")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Enter an event by choosing a category (Participant only).</summary>
    /// <remarks>
    /// Expects: categoryId. The link between the participant, the event and the chosen category is recorded.
    /// A participant can only be enrolled once per event.
    /// </remarks>
    /// <response code="201">Enrolled. Returns the enrolment record and its status.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not a Participant.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">The participant is already enrolled in this event.</response>
    [HttpPost]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(EnrolRequest request)
    {
        var userId = User.GetUserId();
        var category = await _db.Categories.Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return Problem(title: "Category not found.", statusCode: 404);

        if (category.Event.EventDate < Today)
            return Problem(title: "This event has already taken place.", statusCode: 400);

        var alreadyEnrolled = await _db.Enrolments
            .AnyAsync(en => en.ParticipantId == userId && en.Category.EventId == category.EventId);
        if (alreadyEnrolled)
            return Problem(title: "You are already enrolled in this event.", statusCode: 409);

        var enrolment = new Enrolment
        {
            ParticipantId = userId,
            CategoryId = category.CategoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Confirmed"
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var response = await _db.Enrolments.AsNoTracking()
            .Where(en => en.EnrolmentId == enrolment.EnrolmentId)
            .Select(Mappings.EnrolmentToResponse)
            .FirstAsync();
        return Created($"/api/enrolments/my", response);
    }

    /// <summary>List all of your own enrolments, past and upcoming (Participant only).</summary>
    /// <response code="200">Returns your enrolments, newest event first.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not a Participant.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.GetUserId();
        var enrolments = await _db.Enrolments.AsNoTracking()
            .Where(en => en.ParticipantId == userId)
            .OrderByDescending(en => en.Category.Event.EventDate)
            .Select(Mappings.EnrolmentToResponse)
            .ToListAsync();
        return Ok(enrolments);
    }

    /// <summary>Cancel your own enrolment before the event takes place (Participant only).</summary>
    /// <response code="200">Enrolment cancelled.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not a Participant, or the enrolment belongs to someone else.</response>
    /// <response code="404">The enrolment does not exist.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Category).ThenInclude(c => c.Event)
            .Include(en => en.Result)
            .FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null) return Problem(title: "Enrolment not found.", statusCode: 404);
        if (enrolment.ParticipantId != User.GetUserId())
            return Problem(title: "You can only cancel your own enrolments.", statusCode: 403);
        if (enrolment.Category.Event.EventDate < Today)
            return Problem(title: "You cannot cancel after the event has taken place.", statusCode: 400);

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Enrolment cancelled." });
    }
}
