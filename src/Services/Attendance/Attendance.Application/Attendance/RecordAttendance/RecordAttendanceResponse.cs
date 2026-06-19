namespace Attendance.Application.Attendance.RecordAttendance;

/// <summary>Response DTO for the RecordAttendance command.</summary>
public sealed record RecordAttendanceResponse(string RecordId, string Status);
