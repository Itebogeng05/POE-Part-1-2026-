using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

// ---------- Auth ----------
public class RegisterRequest
{
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(100)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = string.Empty;
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class RegisterResponse
{
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

// ---------- Profile ----------
public class UpdateProfileRequest
{
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [MaxLength(20)] public string? ContactNumber { get; set; }
}

public class ProfileResponse
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ---------- Events & categories ----------
public class EventRequest
{
    [Required, MaxLength(100)] public string EventName { get; set; } = string.Empty;
    [Required] public DateOnly? EventDate { get; set; }
    [Required, MaxLength(150)] public string Location { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public string EventType { get; set; } = string.Empty;
    /// <summary>Headline distance of the event in kilometres.</summary>
    [Range(0.01, 1000)] public decimal DistanceKm { get; set; }
}

public class CategoryRequest
{
    [Required, MaxLength(50)] public string CategoryName { get; set; } = string.Empty;
    [Range(0.01, 1000)] public decimal DistanceKm { get; set; }
    [Range(0, 100000)] public decimal EntryFee { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public List<CategoryResponse> Categories { get; set; } = new();
}

// ---------- Enrolments ----------
public class EnrolRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---------- Results ----------
public class CreateResultRequest
{
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }
    [Required] public TimeOnly? FinishTime { get; set; }
    [Required, Range(1, 100000)] public int? Position { get; set; }
}

public class UpdateResultRequest
{
    [Required] public TimeOnly? FinishTime { get; set; }
    [Required, Range(1, 100000)] public int? Position { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
}
