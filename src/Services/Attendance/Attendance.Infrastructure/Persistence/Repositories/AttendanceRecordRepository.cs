using Attendance.Application.Abstractions;
using Attendance.Application.Students.Shared;
using Attendance.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAttendanceRecordRepository.
/// </summary>
internal sealed class AttendanceRecordRepository(AttendanceDbContext ctx) : IAttendanceRecordRepository
{
    public Task AddAsync(AttendanceRecord record, CancellationToken ct = default)
    {
        ctx.AttendanceRecords.Add(record);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> GetByStudentAsync(
        string            studentId,
        CancellationToken ct = default)
    {
        return await ctx.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderBy(r => r.RecordedAt)
            .Select(r => new AttendanceRecordDto(
                r.Id.Value,
                r.StudentId,
                r.Date.ToString("yyyy-MM-dd"),
                r.Status.ToString()))
            .ToListAsync(ct);
    }
}
