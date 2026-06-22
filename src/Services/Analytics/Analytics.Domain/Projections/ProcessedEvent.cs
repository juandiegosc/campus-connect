namespace Analytics.Domain.Projections;

/// <summary>
/// Idempotency / audit log of every integration event ingested by the analytics ETL pipeline.
/// One row per processed event (PK = EventId). Used both to deduplicate (idempotent receiver)
/// and to compute the "events processed" / "failed messages" dashboard metrics.
/// Plain projection model — no domain invariants.
/// </summary>
public sealed class ProcessedEvent
{
    /// <summary>Primary key — the integration event's EventId.</summary>
    public Guid EventId { get; set; }

    /// <summary>Event type name, e.g. "StudentEnrolled", "PaymentConfirmed".</summary>
    public string EventType { get; set; } = default!;

    /// <summary>Primary business entity id carried by the event (student id, payment id, etc.).</summary>
    public string? EntityId { get; set; }

    /// <summary>Correlation id propagated across the message chain.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>When the source event occurred (from the event payload).</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>When the analytics service ingested the event.</summary>
    public DateTime ReceivedAt { get; set; }
}
