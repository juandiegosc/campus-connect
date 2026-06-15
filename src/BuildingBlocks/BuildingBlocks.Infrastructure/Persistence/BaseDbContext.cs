using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Abstract base DbContext for all CampusConnect service contexts.
/// Provides domain event dispatch hooks and documents where to call
/// MassTransit Outbox/Inbox conventions.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    private readonly IPublisher? _domainEventDispatcher;

    protected BaseDbContext(DbContextOptions options, IPublisher? dispatcher = null)
        : base(options)
    {
        _domainEventDispatcher = dispatcher;
    }

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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Phase 1: Save changes.
        var result = await base.SaveChangesAsync(cancellationToken);

        // Phase 2+: Dispatch domain events from tracked AggregateRoot<TId> entities.
        // Example wiring (to be added when concrete aggregates exist):
        // var aggregates = ChangeTracker.Entries<AggregateRoot<TId>>()
        //     .Where(e => e.Entity.DomainEvents.Any())
        //     .Select(e => e.Entity)
        //     .ToList();
        // foreach (var aggregate in aggregates)
        // {
        //     var events = aggregate.DomainEvents.ToList();
        //     aggregate.ClearDomainEvents();
        //     foreach (var domainEvent in events)
        //         if (_domainEventDispatcher is not null)
        //             await _domainEventDispatcher.Publish(domainEvent, ct);
        // }

        return result;
    }
}
