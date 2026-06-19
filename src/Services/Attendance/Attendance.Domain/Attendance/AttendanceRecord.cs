using Attendance.Domain.Attendance.Events;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Domain.Primitives;

namespace Attendance.Domain.Attendance;

/// <summary>
/// Attendance record aggregate root (REQ-AT1-07, REQ-AT1-08).
/// Fully independent from the Incident aggregate — no parent/child, no FK relationship.
/// SchoolId hardcoded "SCH-001" — // TODO multi-tenant (REQ-AT1-15).
/// </summary>
public sealed class AttendanceRecord : AggregateRoot<AttendanceRecordId>
{
    public string           StudentId  { get; private set; } = default!;
    public DateOnly         Date       { get; private set; }
    public AttendanceStatus Status     { get; private set; }
    public DateTime         RecordedAt { get; private set; }
    public string           SchoolId   { get; private set; } = default!; // TODO multi-tenant

    // EF Core parameterless constructor
    private AttendanceRecord() { }

    /// <summary>
    /// Factory: validates invariants, generates ID, raises domain event, returns Result.
    /// </summary>
    public static Result<AttendanceRecord> Record(
        string          studentId,
        DateOnly        date,
        AttendanceStatus status,
        DateTime        nowUtc,
        AttendanceRecordId id)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Length != 26)
            return Result<AttendanceRecord>.Failure(
                Error.Validation("student_id.invalid",
                    $"StudentId '{studentId}' must be a non-empty 26-character ULID string."));

        if (date == DateOnly.MinValue)
            return Result<AttendanceRecord>.Failure(
                Error.Validation("date.invalid", "Date must be a valid date (not MinValue)."));

        var record = new AttendanceRecord
        {
            Id         = id,
            StudentId  = studentId.Trim(),
            Date       = date,
            Status     = status,
            RecordedAt = nowUtc,
            SchoolId   = "SCH-001" // TODO multi-tenant
        };

        record.Raise(new AttendanceRecordedDomainEvent(
            id.Value,
            studentId.Trim(),
            date.ToString("yyyy-MM-dd"),
            status.ToString()));

        return Result<AttendanceRecord>.Success(record);
    }
}
