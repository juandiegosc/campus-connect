namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Integration event published when an incident is reported.
/// Frozen contract (ADR-070, one-way door): exactly 4 application properties.
/// Description MUST NOT appear here — stored locally on the Incident entity, never published.
/// </summary>
public record IncidentReported : IntegrationEvent
{
    /// <summary>ULID of the Incident aggregate (26 chars).</summary>
    public string IncidentId { get; init; } = default!;

    /// <summary>ULID of the student (26 chars).</summary>
    public string StudentId  { get; init; } = default!;

    /// <summary>Free-text incident type.</summary>
    public string Type       { get; init; } = default!;

    /// <summary>Enum name string: "Low", "Medium", or "High".</summary>
    public string Severity   { get; init; } = default!;
}
