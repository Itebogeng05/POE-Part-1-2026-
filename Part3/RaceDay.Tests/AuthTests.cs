using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@raceday.test";

    // ---------- Registration ----------

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Register_WithValidDetails_Returns201AndRole(string role)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Sam", lastName = "Runner", email = NewEmail(), password = TestApi.Password, role });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(role, body.GetProperty("role").GetString());
        Assert.True(body.GetProperty("userId").GetInt32() > 0);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        var body = new { firstName = "Sam", lastName = "Runner", email, password = TestApi.Password, role = "Participant" };

        var first = await client.PostAsJsonAsync("/api/auth/register", body);
        var second = await client.PostAsJsonAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidRole_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Sam", lastName = "Runner", email = NewEmail(), password = TestApi.Password, role = "Admin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithMissingFields_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "", lastName = "", email = "not-an-email", password = "123", role = "Participant" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotThePlainPassword()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Sam", lastName = "Runner", email, password = TestApi.Password, role = "Participant" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(TestApi.Password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt hash prefix
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokenAndRole()
    {
        var user = await TestApi.CreateUserAsync(_factory, "Organiser");

        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Equal("Organiser", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var user = await TestApi.CreateUserAsync(_factory, "Participant");

        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = TestApi.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Authenticated vs unauthenticated access ----------

    [Fact]
    public async Task Profile_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithGarbageToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.real.token");

        var response = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Profile_WithToken_ReturnsOwnProfileWithoutPassword(string role)
    {
        var user = await TestApi.CreateUserAsync(_factory, role);

        var response = await user.Client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal(role, body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.False(body.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task UpdateProfile_ChangesOnlyTheLoggedInUsersDetails()
    {
        var user = await TestApi.CreateUserAsync(_factory, "Participant");
        var other = await TestApi.CreateUserAsync(_factory, "Participant");

        var update = await user.Client.PutAsJsonAsync("/api/users/profile",
            new { firstName = "Updated", lastName = "Name", contactNumber = "0821234567" });
        var otherProfile = await other.Client.GetFromJsonAsync<JsonElement>("/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated", body.GetProperty("firstName").GetString());
        Assert.Equal("0821234567", body.GetProperty("contactNumber").GetString());
        Assert.Equal("Test", otherProfile.GetProperty("firstName").GetString());
    }
}
