using Academic.Application.Abstractions;
using Academic.Application.Students.Shared;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Academic.Application.Students.GetStudentEvents;

/// <summary>
/// Reads student-related outbox events (ADR-036, R10).
/// The IOutboxEventReader port is implemented in Infrastructure reading from ctx.Set&lt;OutboxMessage&gt;().
/// </summary>
public sealed class GetStudentEventsQueryHandler(
    IStudentRepository repo,
    IOutboxEventReader outboxReader)
    : IRequestHandler<GetStudentEventsQuery, Result<IReadOnlyList<StudentEventDto>>>
{
    public async Task<Result<IReadOnlyList<StudentEventDto>>> Handle(
        GetStudentEventsQuery query,
        CancellationToken     cancellationToken)
    {
        StudentId studentId;
        try { studentId = StudentId.Parse(query.StudentId); }
        catch { return Result<IReadOnlyList<StudentEventDto>>.Failure(Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found.")); }

        var student = await repo.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
            return Result<IReadOnlyList<StudentEventDto>>.Failure(
                Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found."));

        var events = await outboxReader.GetEventsForStudentAsync(studentId.Value, cancellationToken);
        return Result<IReadOnlyList<StudentEventDto>>.Success(events);
    }
}
