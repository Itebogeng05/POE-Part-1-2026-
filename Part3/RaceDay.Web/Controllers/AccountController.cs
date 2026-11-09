using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly IRaceDayApi _api;

    public AccountController(IRaceDayApi api) => _api = api;

    private IActionResult HomeFor(SessionUser user) =>
        user.IsOrganiser ? RedirectToAction("Index", "Organiser") : RedirectToAction("Index", "Home");

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        if (CurrentUser is { IsExpired: false } user) return HomeFor(user);

        if (expired) ViewData["Notice"] = "Your session has expired. Please log in again.";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var login = await _api.LoginAsync(model.Email.Trim(), model.Password);
        if (!login.Success || login.Data is null)
        {
            // Wrong password, unknown email or API down: show the API's friendly message, never raw details.
            ModelState.AddModelError(string.Empty, login.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        var token = login.Data;
        var profile = await _api.GetProfileAsync(token.Token);
        var fullName = profile is { Success: true, Data: not null }
            ? $"{profile.Data.FirstName} {profile.Data.LastName}".Trim()
            : model.Email.Trim();

        var expires = token.ExpiresAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(token.ExpiresAt, DateTimeKind.Utc)
            : token.ExpiresAt.ToUniversalTime();

        // The session now holds the user's id and role for every later page.
        var user = new SessionUser(token.UserId, token.Role, fullName, token.Token, expires);
        HttpContext.Session.SignIn(user);

        FlashSuccess($"Welcome back, {(profile.Data?.FirstName ?? fullName)}!");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return HomeFor(user);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentUser is { IsExpired: false } user) return HomeFor(user);
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.RegisterAsync(model.FirstName.Trim(), model.LastName.Trim(), model.Email.Trim(), model.Password, model.Role);
        if (!result.Success)
        {
            if (result.IsConflict)
                ModelState.AddModelError(nameof(model.Email), result.Error ?? "That email address is already registered.");
            else
                AddApiErrors<RegisterViewModel>(result);
            return View(model);
        }

        FlashSuccess("Your account is ready. Log in to get started.");
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Ends the session. POST only, so a link on another site cannot log someone out.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.SignOut();
        FlashSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
