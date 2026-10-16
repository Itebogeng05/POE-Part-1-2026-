using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = RoleNames.Organiser)]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Update a category of an event you own (Organiser only).</summary>
    /// <remarks>Expects: categoryName, distanceKm and entryFee.</remarks>
    /// <response code="200">Returns the updated category.</response>
    /// <response code="400">A field is invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Problem(title: "Category not found.", statusCode: 404);
        if (category.Event.OrganiserId != User.GetUserId())
            return Problem(title: "You can only manage categories of your own events.", statusCode: 403);

        category.CategoryName = request.CategoryName.Trim();
        category.DistanceKm = request.DistanceKm;
        category.EntryFee = request.EntryFee;
        await _db.SaveChangesAsync();

        return Ok(Mappings.CategoryToResponseFunc(category));
    }

    /// <summary>Remove a category from an event you own (Organiser only). Its enrolments are removed too.</summary>
    /// <response code="200">Category deleted.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">The user is not an Organiser, or does not own the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Problem(title: "Category not found.", statusCode: 404);
        if (category.Event.OrganiserId != User.GetUserId())
            return Problem(title: "You can only manage categories of your own events.", statusCode: 403);

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Category deleted." });
    }
}
