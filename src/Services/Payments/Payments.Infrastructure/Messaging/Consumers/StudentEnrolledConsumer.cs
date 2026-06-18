using BuildingBlocks.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;

namespace Payments.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer adapter for StudentEnrolled integration event.
/// Thin bridge: Infrastructure → IStudentReplicaRepository.UpsertAsync (DIRECT call — no MediatR).
///
/// ADR-039: Consumer-as-Adapter pattern.
/// ADR-042: Must be registered in BOTH DependencyInjection.cs AND PaymentsWebApplicationFactory.
/// ADR-043: CorrelationId null fallback — log warning + use MassTransit transport CorrelationId.
/// ADR-057: No MediatR dispatch → no UnitOfWorkBehavior → UpsertAsync commits its own SaveChanges.
///
/// StudentReplica has NO domain invariants — a direct repository upsert is correct here.
/// </summary>
public sealed class StudentEnrolledConsumer(
    IStudentReplicaRepository              replica,
    TimeProvider                           clock,
    ILogger<StudentEnrolledConsumer>       logger) : IConsumer<StudentEnrolled>
{
    public async Task Consume(ConsumeContext<StudentEnrolled> context)
    {
        var msg = context.Message;

        // ADR-043: CorrelationId null fallback — do NOT fault on a missing trace field
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "StudentEnrolled received with null/empty CorrelationId. " +
                "Falling back to MassTransit transport CorrelationId. " +
                "StudentId={StudentId} TransportCorrelationId={TransportCorrelationId}",
                msg.StudentId, context.CorrelationId);

            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        await replica.UpsertAsync(
            msg.StudentId,
            msg.FullName,
            msg.Grade,
            msg.SchoolId,
            clock.GetUtcNow().UtcDateTime,
            context.CancellationToken);

        // No throw path: upsert is idempotent; a transient DB error propagates naturally →
        // MassTransit retry policy → after exhaustion → _error queue (ADR-041).
    }
}
