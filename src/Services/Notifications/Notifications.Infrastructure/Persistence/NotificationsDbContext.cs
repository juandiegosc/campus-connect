using BuildingBlocks.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Notifications bounded context.
/// Inherits domain-event dispatch and IUnitOfWork from BaseDbContext.
/// Outbox tables: OutboxMessage + OutboxState (publishes NotificationSent/NotificationFailed).
/// InboxState: ACTIVE (multiple event consumers present).
/// Connection string key: "Default".
/// </summary>
public sealed class NotificationsDbContext : BaseDbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddInboxStateEntity();
    }
}
