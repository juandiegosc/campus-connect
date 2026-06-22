using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Records AttendanceRecorded in the processed-events log (feeds the "attendance recorded" metric).</summary>
public sealed class AttendanceRecordedConsumer(IAnalyticsRepository repo) : IConsumer<AttendanceRecorded>
{
    public Task Consume(ConsumeContext<AttendanceRecorded> context)
    {
        var m = context.Message;
        return repo.RecordEventAsync(
            m.EventId, nameof(AttendanceRecorded), m.RecordId, m.CorrelationId, m.OccurredAt, context.CancellationToken);
    }
}
