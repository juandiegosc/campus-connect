using Analytics.Domain.Projections;
using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Analytics bounded context (read-model / projections store).
/// Inherits IUnitOfWork from BaseDbContext. The service does not publish integration events,
/// but the MassTransit EF outbox still requires the Outbox/Inbox tables, so all three are added.
/// Connection string key: "Default".
/// </summary>
public sealed class AnalyticsDbContext : BaseDbContext
{
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<StudentProjection> StudentProjections => Set<StudentProjection>();

    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);

        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddInboxStateEntity();
    }
}
