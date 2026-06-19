using Academic.Application.Abstractions;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MediatR;

namespace Academic.Application.Students.SuspendStudent;

/// <summary>
/// Handles SuspendStudentCommand (Phase 4 — ADR-067/068).
/// Flow (publish MUST precede UnitOfWorkBehavior.SaveChanges for atomicity — Gotcha 28):
///   1. Load student by StudentId → 404 if not found
///   2. Guard: Graduated student cannot be suspended → 409 Conflict (before the domain throws)
///   3. student.Suspend() — Active→Suspended or idempotent no-op if already Suspended
///   4. Persist via UpdateAsync (EF change tracking; SaveChanges is UnitOfWorkBehavior's job)
///   5. Publish StudentStatusUpdated (outbox-backed; committed atomically with the UPDATE)
/// CorrelationId is omitted (publisher stamps from context) — mirrors MarkStudentOverdueCommandHandler.
/// </summary>
public sealed class SuspendStudentCommandHandler(
    IStudentRepository         repository,
    IIntegrationEventPublisher integrationPublisher)
    : IRequestHandler<SuspendStudentCommand, Result>
{
    public async Task<Result> Handle(
        SuspendStudentCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Parse(command.StudentId);
        var student = await repository.GetByIdAsync(studentId, cancellationToken);

        if (student is null)
            return Result.Failure(Error.NotFound(
                "student.not_found",
                $"Student '{command.StudentId}' was not found."));

        // Guard: Graduated is the only invalid transition for Suspend (ADR-068).
        if (student.AcademicStatus == AcademicStatus.Graduated)
            return Result.Failure(Error.Conflict(
                "student.already_graduated",
                $"Student '{command.StudentId}' is graduated and cannot be suspended."));

        // Active → Suspended (raises no domain event — ADR-068); already-Suspended is idempotent no-op.
        student.Suspend(DateTime.UtcNow);

        await repository.UpdateAsync(student, cancellationToken);

        // Gotcha 28: publish BEFORE SaveChanges so the OutboxMessage INSERT commits in the same
        // EF transaction as the student UPDATE (UnitOfWorkBehavior calls SaveChanges after this).
        await integrationPublisher.PublishAsync(
            new StudentStatusUpdated
            {
                StudentId       = student.Id.Value,
                AcademicStatus  = student.AcademicStatus.ToString(),
                FinancialStatus = student.FinancialStatus.ToString()
            },
            cancellationToken);

        return Result.Success();
    }
}
