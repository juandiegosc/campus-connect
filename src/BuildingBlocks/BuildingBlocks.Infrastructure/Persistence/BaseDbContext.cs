using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain.Primitives;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Abstract base DbContext for all CampusConnect service contexts.
/// Implements <see cref="IUnitOfWork"/> (ADR-023) so derived contexts can be registered
/// directly as the unit-of-work without an adapter class.
/// Dispatches domain events via MediatR <see cref="IPublisher"/> AFTER the database write
/// succeeds, then clears the events buffer (ADR-018: save → dispatch → clear).
/// </summary>
/// <remarks>
/// Integration events MUST go via MassTransit Outbox — NOT through this dispatcher.
/// <c>IDomainEvent</c> is internal to the service boundary.
/// </remarks>
public abstract class BaseDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher? _domainEventDispatcher;

    /// <summary>
    /// Initializes a new <see cref="BaseDbContext"/> instance.
    /// </summary>
    /// <param name="options">EF Core context options.</param>
    /// <param name="dispatcher">
    /// MediatR <see cref="IPublisher"/> for domain event dispatch. Pass <c>null</c>
    /// in design-time factories or test doubles that do not need event dispatch.
    /// </param>
    protected BaseDbContext(DbContextOptions options, IPublisher? dispatcher = null)
        : base(options)
    {
        _domainEventDispatcher = dispatcher;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Convention: configure snake_case table names in PostgreSQL in derived contexts.
        //
        // Outbox / Inbox — call in each concrete DbContext that participates in messaging:
        //   modelBuilder.AddInboxStateEntity();
        //   modelBuilder.AddOutboxMessageEntity();
        //   modelBuilder.AddOutboxStateEntity();
        //
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Saves all changes, then dispatches pending domain events from tracked aggregate roots.
    /// Order enforced: SAVE → DISPATCH → CLEAR (ADR-018).
    /// If the write throws, no events are dispatched.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. SNAPSHOT aggregates with pending events BEFORE save.
        //    EF may materialize new entity IDs during SaveChanges — snapshot here so
        //    UserId values are already set when we build event payloads.
        var aggregates = ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        // 2. SAVE — if the write fails, the exception propagates and no events are dispatched.
        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 3. DISPATCH — only when a publisher is wired (test doubles can pass null).
        foreach (var aggregate in aggregates)
        {
            // Copy and CLEAR BEFORE awaiting Publish to prevent double-dispatch if a
            // handler triggers another SaveChangesAsync on the same DbContext instance.
            var events = aggregate.DomainEvents.ToArray();
            aggregate.ClearDomainEvents();

            if (_domainEventDispatcher is not null)
            {
                foreach (var @event in events)
                {
                    await _domainEventDispatcher.Publish(@event, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        return result;
    }
}
