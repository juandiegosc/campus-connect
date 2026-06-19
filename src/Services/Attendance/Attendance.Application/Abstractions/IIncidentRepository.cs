using Attendance.Application.Students.Shared;
using Attendance.Domain.Incidents;

namespace Attendance.Application.Abstractions;

/// <summary>
/// Port for Incident persistence (REQ-AT1-23).
/// Returns Application-owned DTOs for read paths.
/// </summary>
public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken ct = default);

    Task<IReadOnlyList<IncidentSummaryDto>> GetByStudentAsync(string studentId, CancellationToken ct = default);
}
