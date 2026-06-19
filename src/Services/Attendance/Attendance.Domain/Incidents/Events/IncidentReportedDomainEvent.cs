using BuildingBlocks.Domain.Events;

namespace Attendance.Domain.Incidents.Events;

/// <summary>
/// Domain event raised when an incident is reported.
/// CRITICAL: Description MUST NOT appear here (REQ-AT1-13, one-way door).
/// Fields: IncidentId, StudentId, Type, Severity only.
/// </summary>
public sealed record IncidentReportedDomainEvent(
    string IncidentId,
    string StudentId,
    string Type,
    string Severity) : IDomainEvent;
