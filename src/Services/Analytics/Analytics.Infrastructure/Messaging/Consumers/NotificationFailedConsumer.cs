using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Records NotificationFailed in the processed-events log (feeds the "failed messages" metric).</summary>
public sealed class NotificationFailedConsumer(IAnalyticsRepository repo) : IConsumer<NotificationFailed>
{
    public Task Consume(ConsumeContext<NotificationFailed> context)
    {
        var m = context.Message;
        return repo.RecordEventAsync(
            m.EventId, nameof(NotificationFailed), m.NotificationId, m.CorrelationId, m.OccurredAt, context.CancellationToken);
    }
}
