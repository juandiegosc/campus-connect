using Academic.Application.Abstractions;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MediatR;

namespace Academic.Application.Students.ConfirmPayment;

/// <summary>
/// Handles ConfirmStudentPaymentCommand.
/// Flow (order is non-negotiable — publish MUST precede UnitOfWorkBehavior.SaveChanges for atomicity):
///   1. Load student by StudentId → 404 failure if not found
///   2. Call student.ConfirmPayment() — idempotent domain method (no-op if already Paid)
///   3. Persist via UpdateAsync (signals EF change tracking; SaveChanges is UnitOfWorkBehavior's job)
///   4. Publish StudentStatusUpdated via IIntegrationEventPublisher (outbox-backed; committed atomically)
///   5. Return Result.Success()
/// UnitOfWorkBehavior commits the EF transaction AFTER this handler returns.
/// </summary>
public sealed class ConfirmStudentPaymentCommandHandler(
    IStudentRepository         repository,
    IIntegrationEventPublisher integrationPublisher)
    : IRequestHandler<ConfirmStudentPaymentCommand, Result>
{
    public async Task<Result> Handle(
        ConfirmStudentPaymentCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load
        var studentId = StudentId.Parse(command.StudentId);
        var student = await repository.GetByIdAsync(studentId, cancellationToken);

        // 2. NotFound check
        if (student is null)
            return Result.Failure(Error.NotFound(
                "student.not_found",
                $"Student '{command.StudentId}' was not found."));

        // 3. Domain call — idempotent: no-op if already Paid
        student.ConfirmPayment(DateTime.UtcNow);

        // 4. Persist (explicit Update mirrors AddAsync style; EF change tracking handles the rest)
        await repository.UpdateAsync(student, cancellationToken);

        // 5. Publish integration event via Application port
        // CRITICAL (Gotcha 3 / R7): PublishAsync MUST be called BEFORE SaveChangesAsync.
        // The MassTransit EF outbox stages the message in memory here; SaveChanges (via
        // UnitOfWorkBehavior) commits both the student UPDATE and the OutboxMessage INSERT atomically.
        await integrationPublisher.PublishAsync(
            new StudentStatusUpdated
            {
                StudentId       = student.Id.Value,
                AcademicStatus  = student.AcademicStatus.ToString(),
                FinancialStatus = student.FinancialStatus.ToString(),
                CorrelationId   = command.CorrelationId
            },
            cancellationToken);

        // 6. Return success — UnitOfWorkBehavior calls SaveChangesAsync after this
        return Result.Success();
    }
}
