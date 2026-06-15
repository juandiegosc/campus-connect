using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Abstractions;

public interface IIntegrationEventFactory
{
    T Create<T>(string correlationId) where T : IntegrationEvent;
}
