using Academic.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.GetStudentEvents;

/// <summary>Query to retrieve outbox events associated with a student (ADR-036, R10).</summary>
public sealed record GetStudentEventsQuery(string StudentId)
    : IQuery<IReadOnlyList<StudentEventDto>>;
