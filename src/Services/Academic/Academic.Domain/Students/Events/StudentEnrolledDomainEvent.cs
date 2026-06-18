using BuildingBlocks.Domain.Events;

namespace Academic.Domain.Students.Events;

/// <summary>
/// Domain event raised when a student is successfully enrolled.
/// Triggers the MassTransit outbox to publish <see cref="BuildingBlocks.Contracts.Events.StudentEnrolled"/>.
/// </summary>
public sealed record StudentEnrolledDomainEvent(
    string StudentId,
    string EnrollmentId,
    string SchoolId,
    string Grade,
    string FullName
) : IDomainEvent;
