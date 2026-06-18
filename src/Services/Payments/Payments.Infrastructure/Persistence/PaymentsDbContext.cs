using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Domain.Obligations;
using Payments.Infrastructure.Persistence.ReadModels;

namespace Payments.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Payments bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from BaseDbContext.
/// Outbox tables: OutboxMessage + OutboxState.
/// InboxState: ACTIVE in Phase 2 (table already created by InitialPayments migration — ADR-055).
/// StudentReplicas: NEW in Phase 2 (ADR-054 read model).
/// Connection string key: "PaymentsDb" (fallback "Default" — Gotcha 6/29).
/// </summary>
public sealed class PaymentsDbContext : BaseDbContext
{
    public DbSet<Obligation>     Obligations     => Set<Obligation>();
    public DbSet<StudentReplica> StudentReplicas => Set<StudentReplica>();

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);

        modelBuilder.AddOutboxMessageEntity();   // PascalCase "OutboxMessage" table (Gotcha 4 from #176)
        modelBuilder.AddOutboxStateEntity();
        // ACTIVE in Phase 2 (ADR-055): InboxState table already exists in DB from InitialPayments migration.
        // Uncommented so EF tracks the model (required for migration snapshot correctness).
        modelBuilder.AddInboxStateEntity();
    }
}
