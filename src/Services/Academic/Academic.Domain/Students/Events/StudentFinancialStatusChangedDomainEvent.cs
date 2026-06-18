using BuildingBlocks.Domain.Events;

namespace Academic.Domain.Students.Events;

/// <summary>Domain event raised when a student's financial status transitions (e.g. Pending → Paid).</summary>
public sealed record StudentFinancialStatusChangedDomainEvent(
    string   StudentId,
    string   OldStatus,
    string   NewStatus,
    DateTime OccurredAt
) : IDomainEvent;
