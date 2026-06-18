using Academic.Domain.Students;

namespace Academic.Application.Abstractions;

/// <summary>
/// Repository port for the Student aggregate.
/// No EF Core or Npgsql references — dependency flows from Infrastructure to Application, not vice versa.
/// </summary>
public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(StudentId id, CancellationToken ct = default);
    Task<Student?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default);
    Task<bool> ExistsByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default);
    Task AddAsync(Student student, CancellationToken ct = default);

    /// <summary>
    /// Marks the student entity as modified so EF Core will emit an UPDATE on SaveChanges.
    /// Called AFTER domain mutations (ConfirmPayment, etc.) but BEFORE UnitOfWorkBehavior.SaveChanges.
    /// Does NOT call SaveChanges — that is the responsibility of UnitOfWorkBehavior.
    /// </summary>
    Task UpdateAsync(Student student, CancellationToken ct = default);

    Task<(IReadOnlyList<Student> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default);
}
