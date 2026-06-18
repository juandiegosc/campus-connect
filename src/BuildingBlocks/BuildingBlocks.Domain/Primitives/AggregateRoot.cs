using BuildingBlocks.Domain.Events;

namespace BuildingBlocks.Domain.Primitives;

/// <summary>
/// Base class for all aggregate roots in CampusConnect 360.
/// Implements <see cref="IAggregateRoot"/> to allow non-generic
/// ChangeTracker iteration in <c>BaseDbContext</c>.
/// </summary>
/// <typeparam name="TId">The aggregate root identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised since last <see cref="ClearDomainEvents"/> call.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Initializes a new <see cref="AggregateRoot{TId}"/> (EF Core parameterless ctor).</summary>
    protected AggregateRoot() { }

    /// <summary>Initializes a new <see cref="AggregateRoot{TId}"/> with the given identifier.</summary>
    /// <param name="id">The aggregate identifier.</param>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Raises a domain event by appending it to the internal buffer.</summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Clears all pending domain events from the buffer.</summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
