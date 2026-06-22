using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Application.Notifications.Shared;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of INotificationRepository.
/// AddAsync only tracks — UnitOfWorkBehavior commits the aggregate together with the outbox message.
/// </summary>
internal sealed class NotificationRepository(NotificationsDbContext ctx) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
        => await ctx.Notifications.AddAsync(notification, ct);

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int take, CancellationToken ct = default)
        => await ctx.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id.Value,
                n.SourceEvent,
                n.StudentId,
                n.Channel.ToString(),
                n.Recipient,
                n.Subject,
                n.Body,
                n.Status.ToString(),
                n.FailureReason,
                n.CreatedAt))
            .ToListAsync(ct);
}
