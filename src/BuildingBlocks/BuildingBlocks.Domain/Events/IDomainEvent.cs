using MediatR;

namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Marker interface for all domain events in CampusConnect 360.
/// Extends <see cref="INotification"/> so events can be dispatched via MediatR's
/// <see cref="IPublisher"/> after a successful database write (ADR-018).
/// </summary>
public interface IDomainEvent : INotification;
