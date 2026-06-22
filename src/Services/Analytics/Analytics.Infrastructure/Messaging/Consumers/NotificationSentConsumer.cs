using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Records NotificationSent in the processed-events log (feeds the "notifications sent" metric).</summary>
public sealed class NotificationSentConsumer(IAnalyticsRepository repo) : IConsumer<NotificationSent>
{
    public Task Consume(ConsumeContext<NotificationSent> context)
    {
        var m = context.Message;
        return repo.RecordEventAsync(
            m.EventId, nameof(NotificationSent), m.NotificationId, m.CorrelationId, m.OccurredAt, context.CancellationToken);
    }
}
