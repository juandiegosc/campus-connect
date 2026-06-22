namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Published by the Notifications service when a (simulated) notification could not be delivered.
/// Stub extended in notifications-service-phase1 — consumed by Analytics for the "failed messages" metric.
/// </summary>
public record NotificationFailed : IntegrationEvent
{
    /// <summary>ULID of the Notification aggregate (26 chars).</summary>
    public string NotificationId { get; init; } = default!;

    /// <summary>Name of the integration event that triggered the notification (e.g. "IncidentReported").</summary>
    public string SourceEvent { get; init; } = default!;

    /// <summary>Notification channel: "Email", "Sms" or "Push".</summary>
    public string Channel { get; init; } = default!;

    /// <summary>Recipient address/handle.</summary>
    public string Recipient { get; init; } = default!;

    /// <summary>Reason the notification failed.</summary>
    public string Reason { get; init; } = default!;
}
