using Analytics.Application.Abstractions;
using BuildingBlocks.Contracts.Events;
using MassTransit;

namespace Analytics.Infrastructure.Messaging.Consumers;

/// <summary>Projects StudentStatusUpdated (academic/financial status) into the student projection.</summary>
public sealed class StudentStatusUpdatedConsumer(IAnalyticsRepository repo) : IConsumer<StudentStatusUpdated>
{
    public Task Consume(ConsumeContext<StudentStatusUpdated> context)
    {
        var m = context.Message;
        return repo.RecordStudentStatusUpdatedAsync(
            m.EventId, m.CorrelationId, m.OccurredAt, m.StudentId, m.AcademicStatus, m.FinancialStatus, context.CancellationToken);
    }
}
