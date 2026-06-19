namespace Attendance.Application.Students.Shared;

/// <summary>
/// Student replica read model DTO for GET /api/attendance/students.
/// </summary>
public sealed record StudentReplicaDto(
    string   StudentId,
    string   FullName,
    string   Grade,
    string   SchoolId,
    DateTime LastUpdatedAt);

/// <summary>
/// Attendance record summary DTO for history view (REQ-AT1-26).
/// </summary>
public sealed record AttendanceRecordDto(
    string  RecordId,
    string  StudentId,
    string  Date,      // ISO "yyyy-MM-dd"
    string  Status);

/// <summary>
/// Incident summary DTO for history view (REQ-AT1-26).
/// Description intentionally excluded (spec ASSUMPTION-SPEC-01, REQ-AT1-26).
/// </summary>
public sealed record IncidentSummaryDto(
    string IncidentId,
    string StudentId,
    string Type,
    string Severity);

/// <summary>
/// Combined history response (REQ-AT1-26, ESC-AT-16).
/// </summary>
public sealed record StudentHistoryDto(
    IReadOnlyList<AttendanceRecordDto> Attendance,
    IReadOnlyList<IncidentSummaryDto>  Incidents);
