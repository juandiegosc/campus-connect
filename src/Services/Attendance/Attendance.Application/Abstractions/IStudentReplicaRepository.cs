using Attendance.Application.Students.Shared;

namespace Attendance.Application.Abstractions;

/// <summary>
/// Port for StudentReplica persistence (REQ-AT1-24).
/// ALL parameters and return types are primitives or Application-owned DTOs (port purity ADR-054).
/// UpsertAsync commits its own SaveChanges (ADR-057 exception — consumer has no UoW pipeline).
/// </summary>
public interface IStudentReplicaRepository
{
    /// <summary>
    /// Insert or update the student replica. COMMITS via SaveChangesAsync (ADR-057).
    /// </summary>
    Task UpsertAsync(
        string studentId,
        string fullName,
        string grade,
        string schoolId,
        DateTime lastUpdatedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if a replica row exists for <paramref name="studentId"/>.
    /// Used by both write handlers for the existence guard (REQ-AT1-21).
    /// </summary>
    Task<bool> ExistsAsync(string studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns all student replicas as DTOs for GET /api/attendance/students.
    /// </summary>
    Task<IReadOnlyList<StudentReplicaDto>> GetAllAsync(CancellationToken ct = default);
}
