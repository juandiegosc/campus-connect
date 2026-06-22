using BuildingBlocks.Application.Common;

namespace Notifications.Domain.Notifications;

/// <summary>
/// Delivery channel for a (simulated) notification.
/// </summary>
public enum NotificationChannel
{
    Email,
    Sms,
    Push
}

public static class NotificationChannelExtensions
{
    public static Result<NotificationChannel> TryCreate(string? raw)
    {
        if (Enum.TryParse<NotificationChannel>(raw, ignoreCase: true, out var value))
            return Result<NotificationChannel>.Success(value);

        return Result<NotificationChannel>.Failure(
            Error.Validation("notification_channel.invalid",
                $"Channel '{raw}' is not valid. Must be one of: {string.Join(", ", Enum.GetNames<NotificationChannel>())}."));
    }
}
