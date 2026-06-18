namespace Academic.Application.Abstractions;

/// <summary>
/// Publishes integration events to the message bus outbox.
/// Application layer port — implemented in Infrastructure with MassTransit
/// to keep the Application layer free of transport dependencies.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class;
}
