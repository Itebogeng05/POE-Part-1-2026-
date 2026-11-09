using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/results")]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture a participant's finishing time and position (Organiser only).</summary>
    /// <remarks>Expects: enrolmentId, finishTime (hh:mm:ss) and position (1 or higher). The enrolment must belong to one of your events.</remarks>
    /// <response code="201">Result captured. Returns the result.</response>
    /// <response code="400">A field is invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own the event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return Problem(title: "Enrolment not found.", statusCode: 404);
        if (enrolment.Category.Event.OrganiserId != User.GetUserId())
            return Problem(title: "You can only capture results for your own events.", statusCode: 403);
        if (await _db.Results.AnyAsync(r => r.EnrolmentId == request.EnrolmentId))
            return Problem(title: "A result has already been captured for this enrolment.", statusCode: 409);

        var result = new Result
        {
            EnrolmentId = request.EnrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = "Final"
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking()
            .Where(r => r.ResultId == result.ResultId)
            .Select(Mappings.ResultToResponse)
            .FirstAsync();
        return Created($"/api/events/{response.EventId}/results", response);
    }

    /// <summary>Correct a previously captured result (Organiser only).</summary>
    /// <remarks>Expects: finishTime (hh:mm:ss) and position.</remarks>
    /// <response code="200">Returns the updated result.</response>
    /// <response code="400">A field is invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own the event.</response>
    /// <response code="404">The result does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Problem(title: "Result not found.", statusCode: 404);
        if (result.Enrolment.Category.Event.OrganiserId != User.GetUserId())
            return Problem(title: "You can only manage results for your own events.", statusCode: 403);

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = "Final";
        await _db.SaveChangesAsync();

        return Ok(await _db.Results.AsNoTracking()
            .Where(r => r.ResultId == id)
            .Select(Mappings.ResultToResponse)
            .FirstAsync());
    }

    /// <summary>View your own results across all events (Participant only).</summary>
    /// <response code="200">Returns your results with event and category details.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not a Participant.</response>
    [HttpGet("my")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.GetUserId();
        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(Mappings.ResultToResponse)
            .ToListAsync();
        return Ok(results);
    }
}
