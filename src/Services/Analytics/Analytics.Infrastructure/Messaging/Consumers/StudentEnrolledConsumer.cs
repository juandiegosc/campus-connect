using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Projects StudentEnrolled into the student projection + processed-events log.</summary>
public sealed class StudentEnrolledConsumer(IAnalyticsRepository repo) : IConsumer<StudentEnrolled>
{
    public Task Consume(ConsumeContext<StudentEnrolled> context)
    {
        var m = context.Message;
        return repo.RecordStudentEnrolledAsync(
            m.EventId, m.CorrelationId, m.OccurredAt, m.StudentId, m.FullName, m.Grade, context.CancellationToken);
    }
}
