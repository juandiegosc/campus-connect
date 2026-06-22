using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Records IncidentReported in the processed-events log (feeds the "incidents reported" metric).</summary>
public sealed class IncidentReportedConsumer(IAnalyticsRepository repo) : IConsumer<IncidentReported>
{
    public Task Consume(ConsumeContext<IncidentReported> context)
    {
        var m = context.Message;
        return repo.RecordEventAsync(
            m.EventId, nameof(IncidentReported), m.IncidentId, m.CorrelationId, m.OccurredAt, context.CancellationToken);
    }
}
