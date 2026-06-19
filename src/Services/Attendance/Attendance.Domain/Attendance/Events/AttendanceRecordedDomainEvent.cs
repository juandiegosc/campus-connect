using BuildingBlocks.Domain.Events;

namespace Attendance.Domain.Attendance.Events;

/// <summary>
/// Domain event raised when an attendance record is created.
/// Documents intent on the aggregate (ADR-075); handler publishes the integration event inline.
/// Fields: RecordId, StudentId, Date (ISO string), Status (string).
/// </summary>
public sealed record AttendanceRecordedDomainEvent(
    string RecordId,
    string StudentId,
    string Date,
    string Status) : IDomainEvent;
