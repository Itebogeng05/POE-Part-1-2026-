using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly ITokenService _tokens;

    public AuthController(RaceDayDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Expects: firstName, lastName, email, password (min 8 characters) and role ("Organiser" or "Participant").
    /// The password is hashed with BCrypt before it is stored; the plain password is never saved.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user ID and role.</response>
    /// <response code="400">A field is missing or invalid, or the role is not Organiser/Participant.</response>
    /// <response code="409">The email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var roleName = new[] { RoleNames.Organiser, RoleNames.Participant }
            .FirstOrDefault(r => string.Equals(r, request.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (roleName is null)
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be either 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Problem(title: "That email address is already registered.", statusCode: StatusCodes.Status409Conflict);

        var role = await _db.Roles.FirstAsync(r => r.RoleName == roleName);
        var user = new User
        {
            RoleId = role.RoleId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Problem(title: "That email address is already registered.", statusCode: StatusCodes.Status409Conflict);
        }

        return Created("/api/users/profile", new RegisterResponse { UserId = user.UserId, Role = role.RoleName });
    }

    /// <summary>Log in and receive a JWT token for all protected requests.</summary>
    /// <remarks>
    /// Expects: email and password. Send the returned token as "Authorization: Bearer {token}".
    /// The token contains the user's ID and role, which keeps the session state across requests.
    /// </remarks>
    /// <response code="200">Login succeeded. Returns token, userId, role and expiry time.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        var (token, expiresAt) = _tokens.CreateToken(user);
        return Ok(new LoginResponse { Token = token, UserId = user.UserId, Role = user.Role.RoleName, ExpiresAt = expiresAt });
    }
}
