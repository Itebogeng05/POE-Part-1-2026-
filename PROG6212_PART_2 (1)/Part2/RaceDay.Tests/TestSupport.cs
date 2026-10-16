using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>Runs the real API in memory, but swaps SQL Server for an in-memory database.</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (existing is not null) services.Remove(existing);

            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

/// <summary>A registered, logged-in test user with an HttpClient that already sends their token.</summary>
public record TestUser(HttpClient Client, int UserId, string Email, string Password, string Role);

public static class TestApi
{
    public const string Password = "Passw0rd!";

    public static DateOnly FutureDate(int days = 30) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days);

    public static async Task<TestUser> CreateUserAsync(RaceDayFactory factory, string role)
    {
        var anonymous = factory.CreateClient();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@raceday.test";

        var register = await anonymous.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Test", lastName = role, email, password = Password, role });
        register.EnsureSuccessStatusCode();

        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());

        return new TestUser(client, body.GetProperty("userId").GetInt32(), email, Password, role);
    }

    public static object EventBody(string name = "Test Run", string type = "Run", DateOnly? date = null) => new
    {
        eventName = name,
        eventDate = date ?? FutureDate(),
        location = "Johannesburg",
        description = "A test event",
        eventType = type,
        distanceKm = 10
    };

    public static async Task<int> CreateEventAsync(HttpClient organiser)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetInt32();
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "10km", distanceKm = 10, entryFee = 120 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("categoryId").GetInt32();
    }

    public static async Task<int> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("enrolmentId").GetInt32();
    }
}
