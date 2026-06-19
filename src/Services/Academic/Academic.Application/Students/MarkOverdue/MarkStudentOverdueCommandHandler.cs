using Academic.Application.Abstractions;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MediatR;

namespace Academic.Application.Students.MarkOverdue;

/// <summary>
/// Handles MarkStudentOverdueCommand (Phase 3 — ADR-063).
/// Flow (publish MUST precede UnitOfWorkBehavior.SaveChanges for atomicity — Gotcha 28):
///   1. Load student by StudentId → 404 if not found
///   2. Guard: a Paid student cannot be marked overdue → 409 Conflict (before the domain throws)
///   3. student.MarkOverdue() — Pending→Overdue or idempotent no-op if already Overdue
///   4. Persist via UpdateAsync (EF change tracking; SaveChanges is UnitOfWorkBehavior's job)
///   5. Publish StudentStatusUpdated (outbox-backed; committed atomically with the UPDATE)
/// CorrelationId is omitted (publisher stamps from context) — mirrors EnrollStudentCommandHandler.
/// </summary>
public sealed class MarkStudentOverdueCommandHandler(
    IStudentRepository         repository,
    IIntegrationEventPublisher integrationPublisher)
    : IRequestHandler<MarkStudentOverdueCommand, Result>
{
    public async Task<Result> Handle(
        MarkStudentOverdueCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Parse(command.StudentId);
        var student = await repository.GetByIdAsync(studentId, cancellationToken);

        if (student is null)
            return Result.Failure(Error.NotFound(
                "student.not_found",
                $"Student '{command.StudentId}' was not found."));

        // ADR-063: a Paid student cannot be marked overdue — guard before the domain throws.
        if (student.FinancialStatus == FinancialStatus.Paid)
            return Result.Failure(Error.Conflict(
                "student.already_paid",
                $"Student '{command.StudentId}' has a Paid financial status and cannot be marked overdue."));

        // Pending → Overdue (raises domain event); already-Overdue is an idempotent no-op.
        student.MarkOverdue(DateTime.UtcNow);

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
