using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

// ---------- Account ----------
public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your last name."), StringLength(50)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like a valid email address."), StringLength(100)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose how you will use RaceDay.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose Organiser or Participant.")]
    [Display(Name = "I want to")]
    public string Role { get; set; } = string.Empty;
}

// ---------- Organiser: events ----------
public class EventFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter an event name."), StringLength(100)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose an event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Event type must be Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    /// <summary>The banner file chosen in the form (optional).</summary>
    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool HasBanner { get; set; }

    public static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };
}

// ---------- Organiser: categories ----------
public class CategoryFormViewModel
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name (for example 10km or Under 20)."), StringLength(50)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please enter the entry fee (0 for free).")]
    [Range(0, 100000, ErrorMessage = "The entry fee must be between 0 and 100 000.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; }
}

public class CategoriesPageViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

// ---------- Organiser: dashboard ----------
public class EventSummary
{
    public EventDto Event { get; set; } = new();
    public int Enrolments { get; set; }
}

public class DashboardViewModel
{
    public string OrganiserName { get; set; } = string.Empty;
    public List<EventSummary> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int UpcomingEvents => Events.Count(e => e.Event.IsUpcoming);
    public int TotalEnrolments => Events.Sum(e => e.Enrolments);
    public List<EventSummary> NextUp => Events.Where(e => e.Event.IsUpcoming).OrderBy(e => e.Event.EventDate).Take(3).ToList();
    public string? Warning { get; set; }
}

// ---------- Organiser: enrolments and results ----------
public class EventPickerViewModel
{
    public List<EventSummary> Events { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public List<EventDto> MyEvents { get; set; } = new();
    public EventDto? Selected { get; set; }
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public EnrolmentDto Enrolment { get; set; } = new();
    public ResultDto? Result { get; set; }
}

public class ResultsPageViewModel
{
    public List<EventDto> MyEvents { get; set; } = new();
    public EventDto? Selected { get; set; }
    public List<ResultRow> Rows { get; set; } = new();
    public bool EventHasFinished { get; set; }
}

public class ResultFormViewModel
{
    [Range(1, int.MaxValue)] public int EventId { get; set; }
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }

    [Required(ErrorMessage = "Enter the finish time as hh:mm:ss.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "Use the format hh:mm:ss, for example 00:47:32.")]
    public string FinishTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the finishing position.")]
    [Range(1, 100000, ErrorMessage = "The position must be a whole number from 1 up.")]
    public int? Position { get; set; }
}

// ---------- Participant ----------
public class EventFilter
{
    public string? Q { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? MinKm { get; set; }
    public decimal? MaxKm { get; set; }

    public bool IsActive => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Type) || From.HasValue || To.HasValue
                            || MinKm.HasValue || MaxKm.HasValue;
}

public class EventListViewModel
{
    public EventFilter Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
    public int TotalUpcoming { get; set; }
    public string UserName { get; set; } = string.Empty;
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
    public List<ResultDto> Results { get; set; } = new();
    public bool CanEnrol { get; set; }
    public bool IsOwner { get; set; }
}

public class MyResultRow
{
    public ResultDto Result { get; set; } = new();
    public DateOnly? EventDate { get; set; }
    public int? TotalFinishers { get; set; }
}
