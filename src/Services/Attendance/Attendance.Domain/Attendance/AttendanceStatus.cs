using BuildingBlocks.Application.Common;

namespace Attendance.Domain.Attendance;

/// <summary>
/// Attendance status enum with exactly three members (REQ-AT1-04).
/// TryCreate returns Result.Failure for unknown values (REQ-AT1-12).
/// </summary>
public enum AttendanceStatus
{
    Present,
    Absent,
    Late
}

public static class AttendanceStatusExtensions
{
    public static Result<AttendanceStatus> TryCreate(string? raw)
    {
        if (Enum.TryParse<AttendanceStatus>(raw, ignoreCase: true, out var value))
            return Result<AttendanceStatus>.Success(value);

        return Result<AttendanceStatus>.Failure(
            Error.Validation("attendance_status.invalid",
                $"Status '{raw}' is not valid. Must be one of: {string.Join(", ", Enum.GetNames<AttendanceStatus>())}."));
    }
}
