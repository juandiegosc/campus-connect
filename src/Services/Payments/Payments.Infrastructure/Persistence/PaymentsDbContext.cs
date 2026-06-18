using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Domain.Obligations;

namespace Payments.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Payments bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from BaseDbContext.
/// Outbox tables: OutboxMessage + OutboxState ONLY (NO InboxState — ADR-046, ESC-PM-26).
/// Connection string key: "PaymentsDb" (fallback "Default" — Gotcha 6/29).
/// </summary>
public sealed class PaymentsDbContext : BaseDbContext
{
    public DbSet<Obligation> Obligations => Set<Obligation>();

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);

        // CRITICAL (ADR-046, ESC-PM-26): Only OutboxMessage + OutboxState — NO InboxState.
        // No consumer in Phase 1; InboxState added additively in Phase 2 when consumer arrives.
        modelBuilder.AddOutboxMessageEntity();   // PascalCase "OutboxMessage" table (Gotcha 4 from #176)
        modelBuilder.AddOutboxStateEntity();
        // modelBuilder.AddInboxStateEntity(); ← intentionally absent in Phase 1
    }
}
