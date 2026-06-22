namespace Notifications.Application.Notifications.RegisterNotification;

/// <summary>Result of registering a notification.</summary>
public sealed record RegisterNotificationResponse(string NotificationId, string Status);
