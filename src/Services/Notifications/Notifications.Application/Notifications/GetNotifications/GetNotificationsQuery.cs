using BuildingBlocks.Application.Messaging;
using Notifications.Application.Notifications.Shared;

namespace Notifications.Application.Notifications.GetNotifications;

/// <summary>
/// Query to list recent notifications for the operator dashboard.
/// </summary>
public sealed record GetNotificationsQuery(int Take = 100)
    : IQuery<IReadOnlyList<NotificationDto>>;
