using System.Net;

namespace RaceDay.Web.Services;

/// <summary>The outcome of one API call. Controllers never see raw HTTP errors, only this.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public HttpStatusCode? Status { get; init; }
    /// <summary>A message that is safe to show to a user.</summary>
    public string? Error { get; init; }
    /// <summary>Field-level problems reported by the API (field name to messages).</summary>
    public Dictionary<string, string[]> FieldErrors { get; init; } = new();

    public bool IsUnauthorized => Status == HttpStatusCode.Unauthorized;
    public bool IsForbidden => Status == HttpStatusCode.Forbidden;
    public bool IsNotFound => Status == HttpStatusCode.NotFound;
    public bool IsConflict => Status == HttpStatusCode.Conflict;
    public bool ServiceUnavailable => Status is null || (int)Status >= 500;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T data, HttpStatusCode status = HttpStatusCode.OK) =>
        new() { Success = true, Data = data, Status = status };

    public static ApiResult<T> From(ApiResult failure) => new()
    {
        Success = false,
        Status = failure.Status,
        Error = failure.Error,
        FieldErrors = failure.FieldErrors
    };
}
