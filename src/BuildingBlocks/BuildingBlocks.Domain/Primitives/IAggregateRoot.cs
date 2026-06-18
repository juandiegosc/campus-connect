using BuildingBlocks.Domain.Events;

namespace BuildingBlocks.Domain.Primitives;

/// <summary>
/// Non-generic marker interface for aggregate roots.
/// Enables ChangeTracker iteration in <c>BaseDbContext.SaveChangesAsync</c>
/// without reflection over the generic type parameter <c>TId</c>.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Domain events raised since the last call to <see cref="ClearDomainEvents"/>.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears the domain events buffer. Called by <c>BaseDbContext</c> after dispatching
    /// all pending events to MediatR's <c>IPublisher</c>.
    /// </summary>
    void ClearDomainEvents();
}
