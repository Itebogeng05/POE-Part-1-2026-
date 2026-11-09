using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Checks that the wrong role (or no login) is rejected on role-based endpoints.</summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);

        var response = await organiser.Client.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantEnrolments_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.GetAsync("/api/enrolments/my");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantResults_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.GetAsync("/api/results/my");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_Returns403()
    {
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = 1, finishTime = "00:30:00", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteCategories_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);

        var update = await participant.Client.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { categoryName = "Hacked", distanceKm = 1, entryFee = 0 });
        var delete = await participant.Client.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotViewEnrolmentsOfSomeoneElsesEvent_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var otherOrganiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var response = await otherOrganiser.Client.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/users/profile")]
    [InlineData("GET", "/api/enrolments/my")]
    [InlineData("GET", "/api/results/my")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("DELETE", "/api/enrolments/1")]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string method, string url)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicEndpoints_WithoutLogin_AreAccessible()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{eventId}/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{eventId}/results")).StatusCode);
    }
}
