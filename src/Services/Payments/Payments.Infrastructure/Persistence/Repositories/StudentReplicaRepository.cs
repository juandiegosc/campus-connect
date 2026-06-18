using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Application.Students.Shared;
using Payments.Infrastructure.Persistence.ReadModels;

namespace Payments.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IStudentReplicaRepository.
/// Port purity: all method signatures use primitives and Application-owned DTOs (ADR-054).
///
/// IMPORTANT — ADR-057 (UoW exception):
/// UpsertAsync calls SaveChangesAsync explicitly. This is the DOCUMENTED EXCEPTION to the
/// codebase-wide rule "repositories never call SaveChanges". The consumer runs OUTSIDE the
/// MediatR UnitOfWorkBehavior pipeline (no MediatR command is dispatched). If SaveChangesAsync
/// were removed, every StudentEnrolled write would be silently discarded.
/// The write is atomic with the InboxState row in the MassTransit EF inbox transaction.
/// </summary>
internal sealed class StudentReplicaRepository(PaymentsDbContext ctx) : IStudentReplicaRepository
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

    public async Task<(IReadOnlyList<StudentReplicaItemDto> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? grade,
        string? search,
        CancellationToken ct = default)
    {
        var q = ctx.StudentReplicas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(grade))
            q = q.Where(s => s.Grade == grade);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => s.FullName.Contains(search));   // EF translates to ILIKE on Postgres

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(s => s.StudentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentReplicaItemDto(
                s.StudentId, s.FullName, s.Grade, s.SchoolId, s.LastUpdatedAt))
            .ToListAsync(ct);

        return (items, total);
    }
}
