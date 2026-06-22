namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Published by the Notifications service when a (simulated) notification is delivered.
/// Stub extended in notifications-service-phase1 — consumed by Analytics for the "events processed" metric.
/// </summary>
public record NotificationSent : IntegrationEvent
{
    /// <summary>ULID of the Notification aggregate (26 chars).</summary>
    public string NotificationId { get; init; } = default!;

    /// <summary>Name of the integration event that triggered the notification (e.g. "StudentEnrolled").</summary>
    public string SourceEvent { get; init; } = default!;

    /// <summary>Notification channel: "Email", "Sms" or "Push".</summary>
    public string Channel { get; init; } = default!;

    /// <summary>Recipient address/handle.</summary>
    public string Recipient { get; init; } = default!;
}
