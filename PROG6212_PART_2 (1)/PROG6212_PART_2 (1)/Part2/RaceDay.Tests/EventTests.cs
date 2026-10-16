using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_WithoutLogin_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    [InlineData("Cycle")]
    public async Task CreateEvent_AsOrganiser_Returns201AndCapturesAllFields(string type)
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestApi.EventBody("City Race", type));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("City Race", body.GetProperty("eventName").GetString());
        Assert.Equal("Johannesburg", body.GetProperty("location").GetString());
        Assert.Equal("A test event", body.GetProperty("description").GetString());
        Assert.Equal(type, body.GetProperty("eventType").GetString());
        Assert.Equal(10, body.GetProperty("distanceKm").GetDecimal());
        Assert.Equal(organiser.UserId, body.GetProperty("organiserId").GetInt32());
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_Returns403()
    {
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/events", TestApi.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", TestApi.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_Returns400()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestApi.EventBody(type: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_Returns400()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events",
            TestApi.EventBody(date: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_ReturnsEventWithCategories()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        await TestApi.CreateCategoryAsync(organiser.Client, eventId);

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("categories").GetArrayLength());
    }

    [Fact]
    public async Task GetEventById_ThatDoesNotExist_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_AsOwner_Returns200AndChangesData()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{eventId}",
            TestApi.EventBody("Renamed Race", "Cycle"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Race", body.GetProperty("eventName").GetString());
        Assert.Equal("Cycle", body.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task UpdateEvent_AsDifferentOrganiser_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var otherOrganiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var response = await otherOrganiser.Client.PutAsJsonAsync($"/api/events/{eventId}", TestApi.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_AsParticipant_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PutAsJsonAsync($"/api/events/{eventId}", TestApi.EventBody("Nope"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsOwner_Returns200AndEventIsGone()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{eventId}");
        var get = await _factory.CreateClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsDifferentOrganiser_Returns403AndEventRemains()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var otherOrganiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var delete = await otherOrganiser.Client.DeleteAsync($"/api/events/{eventId}");
        var get = await _factory.CreateClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsParticipant_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await participant.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_AsOwner_Returns201AndAppearsInCategoryList()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var create = await organiser.Client.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "Under 20", distanceKm = 5, entryFee = 80 });
        var list = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/categories");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Under 20", list[0].GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task AddCategory_AsParticipant_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "Sneaky", distanceKm = 5, entryFee = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var otherOrganiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var response = await otherOrganiser.Client.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "Sneaky", distanceKm = 5, entryFee = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteCategory_AsOwner_Work()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);

        var update = await organiser.Client.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { categoryName = "21km", distanceKm = 21.1, entryFee = 300 });
        var delete = await organiser.Client.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }
}
