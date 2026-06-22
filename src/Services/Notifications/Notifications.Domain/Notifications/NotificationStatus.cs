namespace Notifications.Domain.Notifications;

/// <summary>
/// Lifecycle status of a notification: delivered (Sent) or not delivered (Failed).
/// </summary>
public enum NotificationStatus
{
    Sent,
    Failed
}
