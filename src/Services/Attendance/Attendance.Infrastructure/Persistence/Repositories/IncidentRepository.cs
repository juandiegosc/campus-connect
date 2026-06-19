using Attendance.Application.Abstractions;
using Attendance.Application.Students.Shared;
using Attendance.Domain.Incidents;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IIncidentRepository.
/// IncidentSummaryDto intentionally excludes Description (REQ-AT1-26, ASSUMPTION-SPEC-01).
/// </summary>
internal sealed class IncidentRepository(AttendanceDbContext ctx) : IIncidentRepository
{
    public Task AddAsync(Incident incident, CancellationToken ct = default)
    {
        ctx.Incidents.Add(incident);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<IncidentSummaryDto>> GetByStudentAsync(
        string            studentId,
        CancellationToken ct = default)
    {
        return await ctx.Incidents
            .AsNoTracking()
            .Where(i => i.StudentId == studentId)
            .OrderBy(i => i.ReportedAt)
            .Select(i => new IncidentSummaryDto(
                i.Id.Value,
                i.StudentId,
                i.Type,
                i.Severity.ToString()))
            .ToListAsync(ct);
    }
}
