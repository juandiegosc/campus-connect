using Academic.Application.Abstractions;
using MassTransit;

namespace Academic.Infrastructure.Messaging;

internal sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint endpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
        => endpoint.Publish(integrationEvent, cancellationToken);
}
