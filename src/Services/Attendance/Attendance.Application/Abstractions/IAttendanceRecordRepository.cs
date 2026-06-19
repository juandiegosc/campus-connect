using Attendance.Application.Students.Shared;
using Attendance.Domain.Attendance;

namespace Attendance.Application.Abstractions;

/// <summary>
/// Port for AttendanceRecord persistence (REQ-AT1-22).
/// Returns Application-owned DTOs for read paths.
/// </summary>
public interface IAttendanceRecordRepository
{
    Task AddAsync(AttendanceRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<AttendanceRecordDto>> GetByStudentAsync(string studentId, CancellationToken ct = default);
}
