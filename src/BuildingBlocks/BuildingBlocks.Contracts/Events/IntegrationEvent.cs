namespace BuildingBlocks.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType => GetType().Name;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = default!;
}
