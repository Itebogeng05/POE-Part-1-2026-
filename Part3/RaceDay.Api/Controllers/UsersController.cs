using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>View the profile of the logged-in user (Organiser or Participant).</summary>
    /// <remarks>Expects no body. The user is identified from the token. The password is never returned.</remarks>
    /// <response code="200">Returns the user's profile.</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await ProfileQuery(User.GetUserId());
        return profile is null ? Problem(title: "User not found.", statusCode: 404) : Ok(profile);
    }

    /// <summary>Update the logged-in user's own profile.</summary>
    /// <remarks>Expects: firstName, lastName and an optional contactNumber. Email and role cannot be changed.</remarks>
    /// <response code="200">Returns the updated profile.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Missing or invalid token.</response>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return Problem(title: "User not found.", statusCode: 404);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.ContactNumber = string.IsNullOrWhiteSpace(request.ContactNumber) ? null : request.ContactNumber.Trim();
        await _db.SaveChangesAsync();

        return Ok(await ProfileQuery(userId));
    }

    private Task<ProfileResponse?> ProfileQuery(int userId) =>
        _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new ProfileResponse
            {
                UserId = u.UserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                ContactNumber = u.ContactNumber,
                Role = u.Role.RoleName,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();
}
