using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_AsParticipant_Returns201AndLinkIsRecorded()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);

        var response = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(participant.UserId, body.GetProperty("participantId").GetInt32());
        Assert.Equal(categoryId, body.GetProperty("categoryId").GetInt32());
        Assert.Equal(eventId, body.GetProperty("eventId").GetInt32());

        // The enrolment must really be stored in the database (participant + category + event link).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Enrolments.Include(e => e.Category)
            .SingleAsync(e => e.EnrolmentId == body.GetProperty("enrolmentId").GetInt32());
        Assert.Equal(participant.UserId, stored.ParticipantId);
        Assert.Equal(categoryId, stored.CategoryId);
        Assert.Equal(eventId, stored.Category.EventId);
    }

    [Fact]
    public async Task Enrol_AppearsInMyEnrolments()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await TestApi.EnrolAsync(participant.Client, categoryId);

        var mine = await participant.Client.GetFromJsonAsync<JsonElement>("/api/enrolments/my");

        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(enrolmentId, mine[0].GetProperty("enrolmentId").GetInt32());
    }

    [Fact]
    public async Task Enrol_TwiceInSameEvent_Returns409()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        await TestApi.EnrolAsync(participant.Client, categoryId);

        var second = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_InCategoryThatDoesNotExist_Returns404()
    {
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/enrolments", new { categoryId = 999999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var first = await TestApi.CreateUserAsync(_factory, "Participant");
        var second = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        await TestApi.EnrolAsync(first.Client, categoryId);
        await TestApi.EnrolAsync(second.Client, categoryId);

        var response = await organiser.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await TestApi.EnrolAsync(participant.Client, categoryId);

        var cancel = await participant.Client.DeleteAsync($"/api/enrolments/{enrolmentId}");
        var mine = await participant.Client.GetFromJsonAsync<JsonElement>("/api/enrolments/my");

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0, mine.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var owner = await TestApi.CreateUserAsync(_factory, "Participant");
        var stranger = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await TestApi.EnrolAsync(owner.Client, categoryId);

        var response = await stranger.Client.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
