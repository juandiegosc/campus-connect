using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Commands;

/// <summary>
/// Point-to-Point command SENT (not published) to the Notifications service queue
/// to request an ad-hoc notification. Demonstrates the point-to-point messaging pattern:
/// a single consumer (SendNotificationConsumer) processes each message.
/// </summary>
public record SendNotificationCommand : IntegrationEvent
{
    /// <summary>Recipient address/handle.</summary>
    public string Recipient { get; init; } = default!;

    /// <summary>Notification channel: "Email", "Sms" or "Push".</summary>
    public string Channel { get; init; } = "Email";

    /// <summary>Notification subject/title.</summary>
    public string Subject { get; init; } = default!;

    /// <summary>Notification body.</summary>
    public string Body { get; init; } = default!;
}
