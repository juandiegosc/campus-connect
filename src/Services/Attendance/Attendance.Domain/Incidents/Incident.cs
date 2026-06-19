using Attendance.Domain.Incidents.Events;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Domain.Primitives;

namespace Attendance.Domain.Incidents;

/// <summary>
/// Incident aggregate root (REQ-AT1-09, REQ-AT1-10).
/// Fully INDEPENDENT of AttendanceRecord — no FK, no navigational relationship.
/// Description stored locally but NEVER published in the integration event (REQ-AT1-13, one-way door).
/// SchoolId hardcoded "SCH-001" — // TODO multi-tenant (REQ-AT1-15).
/// </summary>
public sealed class Incident : AggregateRoot<IncidentId>
{
    public string          StudentId   { get; private set; } = default!;
    public string          Type        { get; private set; } = default!;
    public IncidentSeverity Severity   { get; private set; }
    public string          Description { get; private set; } = default!; // stored locally, NOT published
    public DateTime        ReportedAt  { get; private set; }
    public string          SchoolId    { get; private set; } = default!; // TODO multi-tenant

    // EF Core parameterless constructor
    private Incident() { }

    /// <summary>
    /// Factory: validates invariants, generates ID, raises domain event (NO description in event),
    /// returns Result.Success.
    /// </summary>
    public static Result<Incident> Report(
        string          studentId,
        string          type,
        IncidentSeverity severity,
        string          description,
        DateTime        nowUtc,
        IncidentId      id)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Length != 26)
            return Result<Incident>.Failure(
                Error.Validation("student_id.invalid",
                    $"StudentId '{studentId}' must be a non-empty 26-character ULID string."));

        if (string.IsNullOrWhiteSpace(type))
            return Result<Incident>.Failure(
                Error.Validation("incident_type.required", "Incident type is required."));

        if (string.IsNullOrWhiteSpace(description))
            return Result<Incident>.Failure(
                Error.Validation("incident_description.required", "Incident description is required."));

        var incident = new Incident
        {
            Id          = id,
            StudentId   = studentId.Trim(),
            Type        = type.Trim(),
            Severity    = severity,
            Description = description.Trim(),
            ReportedAt  = nowUtc,
            SchoolId    = "SCH-001" // TODO multi-tenant
        };

        // CRITICAL: domain event has NO description (REQ-AT1-13, ESC-AT-24)
        incident.Raise(new IncidentReportedDomainEvent(
            id.Value,
            studentId.Trim(),
            type.Trim(),
            severity.ToString()));

        return Result<Incident>.Success(incident);
    }
}
