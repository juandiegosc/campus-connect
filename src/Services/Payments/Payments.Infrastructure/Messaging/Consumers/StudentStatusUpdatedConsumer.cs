using BuildingBlocks.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;

namespace Payments.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer adapter for StudentStatusUpdated integration event (Phase 3).
/// Thin bridge: Infrastructure → IStudentReplicaRepository.UpdateStatusAsync (DIRECT call — no MediatR).
///
/// ADR-039: Consumer-as-Adapter pattern (mirrors StudentEnrolledConsumer).
/// ADR-042: Must be registered in BOTH DependencyInjection.cs AND PaymentsWebApplicationFactory.
/// ADR-043: CorrelationId null fallback — log warning + use MassTransit transport CorrelationId.
/// ADR-057: No MediatR dispatch → no UnitOfWorkBehavior → UpdateStatusAsync commits its own SaveChanges.
/// ADR-060: A status event for an unknown StudentId is a benign no-op (handled in the repository).
///
/// StudentStatusUpdated is a SECONDARY overlay on a replica owned by StudentEnrolled — it updates,
/// never creates. The status fields are stored verbatim as Academic enum names (ADR-061).
/// </summary>
public sealed class StudentStatusUpdatedConsumer(
    IStudentReplicaRepository                  replica,
    TimeProvider                               clock,
    ILogger<StudentStatusUpdatedConsumer>      logger) : IConsumer<StudentStatusUpdated>
{
    public async Task Consume(ConsumeContext<StudentStatusUpdated> context)
    {
        var msg = context.Message;

        // ADR-043: CorrelationId null fallback — do NOT fault on a missing trace field
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "StudentStatusUpdated received with null/empty CorrelationId. " +
                "Falling back to MassTransit transport CorrelationId. " +
                "StudentId={StudentId} TransportCorrelationId={TransportCorrelationId}",
                msg.StudentId, context.CorrelationId);

            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        // ADR-060: no-op + WARNING if the replica row does not exist (handled inside the repository).
        await replica.UpdateStatusAsync(
            msg.StudentId,
            msg.AcademicStatus,
            msg.FinancialStatus,
            clock.GetUtcNow().UtcDateTime,
            context.CancellationToken);
    }
}
