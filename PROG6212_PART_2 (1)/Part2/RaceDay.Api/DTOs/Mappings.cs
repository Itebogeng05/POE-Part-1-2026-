using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

/// <summary>EF-translatable projections so controllers never expose entities (or password hashes) directly.</summary>
public static class Mappings
{
    public static readonly Expression<Func<Event, EventResponse>> EventToResponse = e => new EventResponse
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        EventName = e.EventName,
        EventDate = e.EventDate,
        Location = e.Location,
        Description = e.Description,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        Categories = e.Categories.OrderBy(c => c.DistanceKm).Select(c => new CategoryResponse
        {
            CategoryId = c.CategoryId,
            EventId = c.EventId,
            CategoryName = c.CategoryName,
            DistanceKm = c.DistanceKm,
            EntryFee = c.EntryFee
        }).ToList()
    };

    public static readonly Func<Event, EventResponse> EventToResponseFunc = EventToResponse.Compile();

    public static readonly Expression<Func<Category, CategoryResponse>> CategoryToResponse = c => new CategoryResponse
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee
    };

    public static readonly Func<Category, CategoryResponse> CategoryToResponseFunc = CategoryToResponse.Compile();

    public static readonly Expression<Func<Enrolment, EnrolmentResponse>> EnrolmentToResponse = en => new EnrolmentResponse
    {
        EnrolmentId = en.EnrolmentId,
        ParticipantId = en.ParticipantId,
        ParticipantName = en.Participant.FirstName + " " + en.Participant.LastName,
        ParticipantEmail = en.Participant.Email,
        CategoryId = en.CategoryId,
        CategoryName = en.Category.CategoryName,
        EventId = en.Category.EventId,
        EventName = en.Category.Event.EventName,
        EventDate = en.Category.Event.EventDate,
        EnrolmentDate = en.EnrolmentDate,
        Status = en.Status
    };

    public static readonly Expression<Func<Result, ResultResponse>> ResultToResponse = r => new ResultResponse
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.FirstName + " " + r.Enrolment.Participant.LastName,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.EventName,
        CategoryName = r.Enrolment.Category.CategoryName,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status
    };
}
