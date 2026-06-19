using Academic.Application.Abstractions;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MediatR;

namespace Academic.Application.Students.GraduateStudent;

/// <summary>
/// Handles GraduateStudentCommand (Phase 4 — ADR-066/067/068).
/// Graduate is TERMINAL: already-Graduated → 409 Conflict (NOT idempotent no-op, ADR-066).
/// Flow (publish MUST precede UnitOfWorkBehavior.SaveChanges for atomicity — Gotcha 28):
///   1. Load student by StudentId → 404 if not found
///   2. Guard: already Graduated → 409 Conflict (before the domain throws, REQ-AC4-13)
///   3. student.Graduate() — Active|Suspended → Graduated
///   4. Persist via UpdateAsync (EF change tracking; SaveChanges is UnitOfWorkBehavior's job)
///   5. Publish StudentStatusUpdated (outbox-backed; committed atomically with the UPDATE)
/// CorrelationId is omitted (publisher stamps from context) — mirrors MarkStudentOverdueCommandHandler.
/// </summary>
public sealed class GraduateStudentCommandHandler(
    IStudentRepository         repository,
    IIntegrationEventPublisher integrationPublisher)
    : IRequestHandler<GraduateStudentCommand, Result>
{
    public async Task<Result> Handle(
        GraduateStudentCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Parse(command.StudentId);
        var student = await repository.GetByIdAsync(studentId, cancellationToken);

        if (student is null)
            return Result.Failure(Error.NotFound(
                "student.not_found",
                $"Student '{command.StudentId}' was not found."));

        // Guard: already-Graduated is TERMINAL — return 409 Conflict (ADR-066, REQ-AC4-13).
        if (student.AcademicStatus == AcademicStatus.Graduated)
            return Result.Failure(Error.Conflict(
                "student.already_graduated",
                $"Student '{command.StudentId}' is already graduated."));

        // Active | Suspended → Graduated (raises no domain event — ADR-068).
        student.Graduate(DateTime.UtcNow);

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
