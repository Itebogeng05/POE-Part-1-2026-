using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private async Task<(TestUser Organiser, TestUser Participant, int EventId, int EnrolmentId)> SetupAsync()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        var categoryId = await TestApi.CreateCategoryAsync(organiser.Client, eventId);
        var enrolmentId = await TestApi.EnrolAsync(participant.Client, categoryId);
        return (organiser, participant, eventId, enrolmentId);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_ParticipantCanViewIt()
    {
        var s = await SetupAsync();

        var create = await s.Organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = s.EnrolmentId, finishTime = "00:45:30", position = 2 });
        var mine = await s.Participant.Client.GetFromJsonAsync<JsonElement>("/api/results/my");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(2, mine[0].GetProperty("position").GetInt32());
        Assert.Equal("00:45:30", mine[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var s = await SetupAsync();
        var body = new { enrolmentId = s.EnrolmentId, finishTime = "00:45:30", position = 2 };
        await s.Organiser.Client.PostAsJsonAsync("/api/results", body);

        var second = await s.Organiser.Client.PostAsJsonAsync("/api/results", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForUnknownEnrolment_Returns404()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = 999999, finishTime = "00:45:30", position = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_AsOrganiserWhoDoesNotOwnTheEvent_Returns403()
    {
        var s = await SetupAsync();
        var otherOrganiser = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await otherOrganiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = s.EnrolmentId, finishTime = "00:45:30", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var s = await SetupAsync();
        var create = await s.Organiser.Client.PostAsJsonAsync("/api/results",
            new { enrolmentId = s.EnrolmentId, finishTime = "00:45:30", position = 5 });
        var resultId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resultId").GetInt32();

        var update = await s.Organiser.Client.PutAsJsonAsync($"/api/results/{resultId}",
            new { finishTime = "00:44:10", position = 3 });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task EventResults_AreOrderedByPosition()
    {
        var s = await SetupAsync();
        var second = await TestApi.CreateUserAsync(_factory, "Participant");
        var categoryList = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{s.EventId}/categories");
        var categoryId = categoryList[0].GetProperty("categoryId").GetInt32();
        var secondEnrolment = await TestApi.EnrolAsync(second.Client, categoryId);

        await s.Organiser.Client.PostAsJsonAsync("/api/results", new { enrolmentId = s.EnrolmentId, finishTime = "00:50:00", position = 2 });
        await s.Organiser.Client.PostAsJsonAsync("/api/results", new { enrolmentId = secondEnrolment, finishTime = "00:40:00", position = 1 });

        var leaderboard = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{s.EventId}/results");

        Assert.Equal(2, leaderboard.GetArrayLength());
        Assert.Equal(1, leaderboard[0].GetProperty("position").GetInt32());
        Assert.Equal(2, leaderboard[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task ParticipantWithNoResults_SeesEmptyList()
    {
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");

        var mine = await participant.Client.GetFromJsonAsync<JsonElement>("/api/results/my");

        Assert.Equal(0, mine.GetArrayLength());
    }
}
