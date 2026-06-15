using BuildingBlocks.Contracts.Abstractions;
using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Infrastructure.Time;

public sealed class IntegrationEventFactory(TimeProvider timeProvider) : IIntegrationEventFactory
{
    /// <summary>
    /// Creates an IntegrationEvent of type T, stamping EventId, OccurredAt (from TimeProvider),
    /// and CorrelationId. All command/event creation in handlers MUST go through this factory.
    /// </summary>
    public T Create<T>(string correlationId) where T : IntegrationEvent
    {
        var instance = Activator.CreateInstance<T>();

        // Records are immutable — use 'with' expression to set the init-only properties.
        return instance with
        {
            EventId = Guid.NewGuid(),
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = correlationId
        };
    }
}
