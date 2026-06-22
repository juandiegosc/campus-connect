using Notifications.Application.Notifications.Shared;
using Notifications.Domain.Notifications;

namespace Notifications.Application.Abstractions;

/// <summary>
/// Port for Notification persistence. All parameters/returns are primitives or
/// Application-owned DTOs (port purity). AddAsync only tracks — the MediatR
/// UnitOfWorkBehavior commits the aggregate together with the outbox message.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Tracks a new notification for insertion (commit happens in the UoW behavior).</summary>
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Returns the most recent notifications (newest first) as DTOs.</summary>
    Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int take, CancellationToken ct = default);
}
