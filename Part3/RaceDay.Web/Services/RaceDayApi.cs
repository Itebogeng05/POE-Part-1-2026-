using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Everything the MVC app can ask the Part 2 API to do. The MVC project has no other data source.</summary>
public interface IRaceDayApi
{
    // Auth
    Task<ApiResult<LoginResponse>> LoginAsync(string email, string password);
    Task<ApiResult> RegisterAsync(string firstName, string lastName, string email, string password, string role);
    Task<ApiResult<ProfileDto>> GetProfileAsync(string? token = null);

    // Events and categories
    Task<ApiResult<List<EventDto>>> GetEventsAsync(bool upcomingOnly = false);
    Task<ApiResult<EventDto>> GetEventAsync(int id);
    Task<ApiResult<EventDto>> CreateEventAsync(object request);
    Task<ApiResult<EventDto>> UpdateEventAsync(int id, object request);
    Task<ApiResult> DeleteEventAsync(int id);
    Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, Stream file, string fileName, string contentType);
    Task<ApiResult<ImageFile>> GetBannerAsync(int eventId);
    Task<ApiResult<CategoryDto>> AddCategoryAsync(int eventId, object request);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, object request);
    Task<ApiResult> DeleteCategoryAsync(int categoryId);

    // Enrolments
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult> CancelEnrolmentAsync(int enrolmentId);

    // Results
    Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId);
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
    Task<ApiResult<ResultDto>> CreateResultAsync(int enrolmentId, TimeOnly finishTime, int position);
    Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, TimeOnly finishTime, int position);
}

public class RaceDayApi : IRaceDayApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Unreachable = "We couldn't reach the RaceDay service. Please try again in a moment.";
    private const string Generic = "Something went wrong. Please try again.";

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ILogger<RaceDayApi> _logger;

    public RaceDayApi(HttpClient http, IHttpContextAccessor context, ILogger<RaceDayApi> logger)
    {
        _http = http;
        _context = context;
        _logger = logger;
    }

    // ---------- Auth ----------
    public Task<ApiResult<LoginResponse>> LoginAsync(string email, string password) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", JsonContent.Create(new { email, password }, options: Json), authenticated: false);

    public async Task<ApiResult> RegisterAsync(string firstName, string lastName, string email, string password, string role) =>
        await SendAsync<object>(HttpMethod.Post, "api/auth/register",
            JsonContent.Create(new { firstName, lastName, email, password, role }, options: Json), authenticated: false, readBody: false);

    public Task<ApiResult<ProfileDto>> GetProfileAsync(string? token = null) =>
        SendAsync<ProfileDto>(HttpMethod.Get, "api/users/profile", tokenOverride: token);

    // ---------- Events and categories ----------
    public Task<ApiResult<List<EventDto>>> GetEventsAsync(bool upcomingOnly = false) =>
        SendAsync<List<EventDto>>(HttpMethod.Get, upcomingOnly ? "api/events?upcomingOnly=true" : "api/events", authenticated: false);

    public Task<ApiResult<EventDto>> GetEventAsync(int id) =>
        SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}", authenticated: false);

    public Task<ApiResult<EventDto>> CreateEventAsync(object request) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", JsonContent.Create(request, options: Json));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, object request) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", JsonContent.Create(request, options: Json));

    public async Task<ApiResult> DeleteEventAsync(int id) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/events/{id}", readBody: false);

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, Stream file, string fileName, string contentType)
    {
        var filePart = new StreamContent(file);
        filePart.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { filePart, "file", fileName } };
        return SendAsync<EventDto>(HttpMethod.Post, $"api/events/{eventId}/banner", form);
    }

    public async Task<ApiResult<ImageFile>> GetBannerAsync(int eventId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/events/{eventId}/banner");
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return ApiResult<ImageFile>.From(await FailureAsync(response));

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var type = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return ApiResult<ImageFile>.Ok(new ImageFile(bytes, type));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Banner request failed.");
            return ApiResult<ImageFile>.From(new ApiResult { Error = Unreachable });
        }
    }

    public Task<ApiResult<CategoryDto>> AddCategoryAsync(int eventId, object request) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", JsonContent.Create(request, options: Json));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, object request) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", JsonContent.Create(request, options: Json));

    public async Task<ApiResult> DeleteCategoryAsync(int categoryId) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/categories/{categoryId}", readBody: false);

    // ---------- Enrolments ----------
    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, "api/enrolments", JsonContent.Create(new { categoryId }, options: Json));

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/my");

    public async Task<ApiResult> CancelEnrolmentAsync(int enrolmentId) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}", readBody: false);

    // ---------- Results ----------
    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results", authenticated: false);

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/results/my");

    public Task<ApiResult<ResultDto>> CreateResultAsync(int enrolmentId, TimeOnly finishTime, int position) =>
        SendAsync<ResultDto>(HttpMethod.Post, "api/results",
            JsonContent.Create(new { enrolmentId, finishTime, position }, options: Json));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, TimeOnly finishTime, int position) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}",
            JsonContent.Create(new { finishTime, position }, options: Json));

    // ---------- plumbing ----------

    /// <summary>
    /// Sends one request. Network problems, timeouts and error responses all become an ApiResult with a
    /// friendly message, so a broken API can never crash a page or show a raw error to the user.
    /// </summary>
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null,
        bool authenticated = true, bool readBody = true, string? tokenOverride = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };

            var token = tokenOverride ?? (authenticated ? _context.HttpContext?.Session.GetUser()?.Token : null);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return ApiResult<T>.From(await FailureAsync(response));

            if (!readBody)
                return new ApiResult<T> { Success = true, Status = response.StatusCode };

            var data = await response.Content.ReadFromJsonAsync<T>(Json);
            return data is null
                ? ApiResult<T>.From(new ApiResult { Status = response.StatusCode, Error = Generic })
                : ApiResult<T>.Ok(data, response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "API call {Method} {Url} failed.", method, url);
            return ApiResult<T>.From(new ApiResult { Error = Unreachable });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "API call {Method} {Url} returned unreadable JSON.", method, url);
            return ApiResult<T>.From(new ApiResult { Error = Generic });
        }
    }

    /// <summary>Turns an API error response (ProblemDetails / ValidationProblemDetails) into a friendly ApiResult.</summary>
    private static async Task<ApiResult> FailureAsync(HttpResponseMessage response)
    {
        string? title = null;
        var fieldErrors = new Dictionary<string, string[]>();

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                        title = t.GetString();

                    if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in errors.EnumerateObject())
                        {
                            fieldErrors[field.Name] = field.Value.ValueKind == JsonValueKind.Array
                                ? field.Value.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                                : Array.Empty<string>();
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // The body was not JSON (for example a proxy error page). Fall back to the generic message below.
        }

        var status = response.StatusCode;
        string message;
        if (fieldErrors.Count > 0)
            message = "Please check the highlighted fields and try again.";
        else if ((int)status >= 500)
            message = status == HttpStatusCode.ServiceUnavailable && !string.IsNullOrWhiteSpace(title) ? title : Generic;
        else if (!string.IsNullOrWhiteSpace(title))
            message = title;
        else
            message = status switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Please log in again.",
                HttpStatusCode.Forbidden => "You don't have permission to do that.",
                HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
                _ => Generic
            };

        return new ApiResult { Status = status, Error = message, FieldErrors = fieldErrors };
    }
}
