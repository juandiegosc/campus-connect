using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Records PaymentConfirmed in the processed-events log (feeds the "payments confirmed" metric).</summary>
public sealed class PaymentConfirmedConsumer(IAnalyticsRepository repo) : IConsumer<PaymentConfirmed>
{
    public Task Consume(ConsumeContext<PaymentConfirmed> context)
    {
        var m = context.Message;
        return repo.RecordEventAsync(
            m.EventId, nameof(PaymentConfirmed), m.PaymentId, m.CorrelationId, m.OccurredAt, context.CancellationToken);
    }
}
