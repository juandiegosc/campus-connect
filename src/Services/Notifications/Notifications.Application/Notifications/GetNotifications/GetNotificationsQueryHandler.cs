using BuildingBlocks.Application.Common;
using MediatR;
using Notifications.Application.Abstractions;
using Notifications.Application.Notifications.Shared;

namespace Notifications.Application.Notifications.GetNotifications;

/// <summary>Handler for GetNotificationsQuery.</summary>
public sealed class GetNotificationsQueryHandler(INotificationRepository repo)
    : IRequestHandler<GetNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var take = query.Take is <= 0 or > 500 ? 100 : query.Take;
        var list = await repo.GetRecentAsync(take, cancellationToken);
        return Result<IReadOnlyList<NotificationDto>>.Success(list);
    }
}
