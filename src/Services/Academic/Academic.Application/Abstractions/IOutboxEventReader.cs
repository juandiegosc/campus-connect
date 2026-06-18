using Academic.Application.Students.Shared;

namespace Academic.Application.Abstractions;

/// <summary>
/// Port for reading outbox events for a specific student.
/// Implemented in Infrastructure using EF Core direct access to outbox_message table (ADR-036, R10).
/// </summary>
public interface IOutboxEventReader
{
    Task<IReadOnlyList<StudentEventDto>> GetEventsForStudentAsync(string studentId, CancellationToken ct = default);
}
