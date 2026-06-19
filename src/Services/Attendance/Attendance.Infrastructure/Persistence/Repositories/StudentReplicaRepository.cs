using Attendance.Application.Abstractions;
using Attendance.Application.Students.Shared;
using Attendance.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IStudentReplicaRepository.
/// Port purity: all method signatures use primitives and Application-owned DTOs (ADR-054).
///
/// IMPORTANT — ADR-057 (UoW exception):
/// UpsertAsync calls SaveChangesAsync explicitly. The consumer runs OUTSIDE the
/// MediatR UnitOfWorkBehavior pipeline (no MediatR command is dispatched). Without
/// SaveChangesAsync, every StudentEnrolled write would be silently discarded.
/// The write is atomic with the InboxState row in the MassTransit EF inbox transaction.
/// </summary>
internal sealed class StudentReplicaRepository(AttendanceDbContext ctx) : IStudentReplicaRepository
{
    public async Task UpsertAsync(
        string studentId,
        string fullName,
        string grade,
        string schoolId,
        DateTime lastUpdatedAt,
        CancellationToken ct = default)
    {
        var existing = await ctx.StudentReplicas.FindAsync([studentId], ct);

        if (existing is null)
        {
            ctx.StudentReplicas.Add(new StudentReplica
            {
                StudentId     = studentId,
                FullName      = fullName,
                Grade         = grade,
                SchoolId      = schoolId,
                LastUpdatedAt = lastUpdatedAt
            });
        }
        else
        {
            existing.FullName      = fullName;
            existing.Grade         = grade;
            existing.SchoolId      = schoolId;
            existing.LastUpdatedAt = lastUpdatedAt;
        }

        // Do NOT remove SaveChangesAsync — consumer runs outside UoW pipeline (ADR-057).
        // This write is atomic with InboxState in the MassTransit EF inbox transaction.
        await ctx.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsAsync(string studentId, CancellationToken ct = default)
        => ctx.StudentReplicas.AnyAsync(s => s.StudentId == studentId, ct);

    public async Task<IReadOnlyList<StudentReplicaDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await ctx.StudentReplicas
            .AsNoTracking()
            .OrderBy(s => s.StudentId)
            .Select(s => new StudentReplicaDto(
                s.StudentId,
                s.FullName,
                s.Grade,
                s.SchoolId,
                s.LastUpdatedAt))
            .ToListAsync(ct);
    }
}
