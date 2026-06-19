using Payments.Application.Students.Shared;

namespace Payments.Application.Abstractions;

/// <summary>
/// Port for StudentReplica persistence.
/// ALL parameters and return types are primitives or Application-owned DTOs — NO Infrastructure POCOs,
/// NO IQueryable. Honoring ADR-054 (port purity).
///
/// IMPORTANT: UpsertAsync commits its own SaveChanges (ADR-057).
/// This is an exception to the codebase-wide "repositories never call SaveChanges" rule.
/// The consumer runs OUTSIDE the MediatR UnitOfWorkBehavior pipeline — no UoW will commit for it.
/// </summary>
public interface IStudentReplicaRepository
{
    /// <summary>
    /// Insert or overwrite the student replica for <paramref name="studentId"/>.
    /// COMMITS internally via SaveChangesAsync (ADR-057) — consumer has no UoW pipeline.
    /// Atomic with InboxState row in the MassTransit EF inbox transaction.
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
    /// Used by RegisterObligationCommandHandler existence guard (ADR-056).
    /// </summary>
    Task<bool> ExistsAsync(string studentId, CancellationToken ct = default);

    /// <summary>
    /// Updates the academic + financial status of an existing replica row (Phase 3).
    /// NO-OP + WARNING if no row exists for <paramref name="studentId"/> (ADR-060) — never creates a ghost row.
    /// COMMITS internally via SaveChangesAsync (ADR-057) — consumer has no UoW pipeline.
    /// </summary>
    Task UpdateStatusAsync(
        string studentId,
        string academicStatus,
        string financialStatus,
        DateTime lastUpdatedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Paginated read for GET /api/payments/students.
    /// Returns Application-owned DTOs + total count (port purity — ADR-054).
    /// </summary>
    Task<(IReadOnlyList<StudentReplicaItemDto> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? grade,
        string? search,
        CancellationToken ct = default);
}
