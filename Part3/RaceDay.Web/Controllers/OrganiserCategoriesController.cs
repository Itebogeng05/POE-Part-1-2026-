using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("Organiser/Events/{eventId:int}/Categories")]
public class OrganiserCategoriesController : OrganiserBaseController
{
    public OrganiserCategoriesController(IRaceDayApi api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, redirect) = await LoadOwnedEventAsync(eventId);
        if (redirect is not null) return redirect;

        return View(new CategoriesPageViewModel { Event = ev!, NewCategory = new CategoryFormViewModel { EventId = eventId } });
    }

    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int eventId, CategoryFormViewModel model)
    {
        var (ev, redirect) = await LoadOwnedEventAsync(eventId);
        if (redirect is not null) return redirect;
        model.EventId = eventId;

        if (!ModelState.IsValid)
            return View("Index", new CategoriesPageViewModel { Event = ev!, NewCategory = model });

        var result = await Api.AddCategoryAsync(eventId, ToRequest(model));
        if (Expired(result) is { } expired) return expired;
        if (!result.Success)
        {
            AddApiErrors<CategoryFormViewModel>(result);
            return View("Index", new CategoriesPageViewModel { Event = ev!, NewCategory = model });
        }

        FlashSuccess($"Category \"{result.Data!.CategoryName}\" added.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var (ev, redirect) = await LoadOwnedEventAsync(eventId);
        if (redirect is not null) return redirect;

        var category = ev!.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFoundPage();

        ViewData["EventName"] = ev.EventName;
        return View(new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            EventId = eventId,
            CategoryName = category.CategoryName,
            DistanceKm = category.DistanceKm,
            EntryFee = category.EntryFee
        });
    }

    [HttpPost("{id:int}/Edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormViewModel model)
    {
        var (ev, redirect) = await LoadOwnedEventAsync(eventId);
        if (redirect is not null) return redirect;
        if (ev!.Categories.All(c => c.CategoryId != id)) return NotFoundPage();

        model.CategoryId = id;
        model.EventId = eventId;
        ViewData["EventName"] = ev.EventName;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateCategoryAsync(id, ToRequest(model));
        if (Expired(result) is { } expired) return expired;
        if (!result.Success)
        {
            AddApiErrors<CategoryFormViewModel>(result);
            return View(model);
        }

        FlashSuccess($"Category \"{result.Data!.CategoryName}\" updated.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var (_, redirect) = await LoadOwnedEventAsync(eventId);
        if (redirect is not null) return redirect;

        var result = await Api.DeleteCategoryAsync(id);
        if (Expired(result) is { } expired) return expired;

        if (result.Success) FlashSuccess("Category removed.");
        else FlashError(result.Error ?? "We couldn't remove that category.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private static object ToRequest(CategoryFormViewModel m) => new
    {
        categoryName = m.CategoryName.Trim(),
        distanceKm = m.DistanceKm!.Value,
        entryFee = m.EntryFee!.Value
    };
}
