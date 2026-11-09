using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Part 3: banner images are stored through the API (Azure Blob Storage behind a fake in tests).</summary>
public class BannerTests : IClassFixture<RaceDayFactory>
{
    // Smallest valid PNG header bytes plus padding - enough for the API's file-signature check.
    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    private readonly RaceDayFactory _factory;

    public BannerTests(RaceDayFactory factory) => _factory = factory;

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task UploadBanner_AsOwner_StoresImageAndEventReportsHasBanner()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasBanner").GetBoolean());
    }

    [Fact]
    public async Task GetBanner_Public_ReturnsTheUploadedImage()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        await organiser.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "banner.png", "image/png"));

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetBanner_WhenEventHasNoBanner_Returns404()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithoutLogin_Returns401()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await _factory.CreateClient().PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsParticipant_Returns403()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory, "Participant");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var other = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var response = await other.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithWrongFileType_Returns400()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/events/{eventId}/banner",
            FileContent(new byte[] { 1, 2, 3, 4 }, "malware.exe", "application/octet-stream"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WhenFileIsNotReallyAnImage_Returns400()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/events/{eventId}/banner",
            FileContent(System.Text.Encoding.UTF8.GetBytes("this is just text"), "fake.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithNoFile_Returns400()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/events/{eventId}/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplacingBanner_RemovesTheOldImageFromStorage()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        await organiser.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "one.png", "image/png"));
        var afterFirst = _factory.Blobs.Count;

        await organiser.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "two.png", "image/png"));

        Assert.Equal(afterFirst, _factory.Blobs.Count);
    }

    [Fact]
    public async Task DeletingEvent_AlsoDeletesItsBannerFromStorage()
    {
        var organiser = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(organiser.Client);
        await organiser.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(PngBytes, "b.png", "image/png"));
        var before = _factory.Blobs.Count;

        var response = await organiser.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before - 1, _factory.Blobs.Count);
    }
}
