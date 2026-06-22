using BuildingBlocks.Application.Messaging;

namespace Notifications.Application.Notifications.RegisterNotification;

/// <summary>
/// Internal command dispatched by every MassTransit consumer to create a (simulated) notification.
/// ICommand trigger activates UnitOfWorkBehavior so the Notification row and the outbox message
/// (NotificationSent / NotificationFailed) commit atomically.
/// </summary>
public sealed record RegisterNotificationCommand(
    string SourceEvent,
    string? StudentId,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string CorrelationId) : ICommand<RegisterNotificationResponse>;
