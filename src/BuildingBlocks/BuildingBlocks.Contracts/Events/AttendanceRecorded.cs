namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Integration event published when an attendance record is created.
/// Frozen contract (ADR-070, one-way door): exactly 4 application properties.
/// Description MUST NOT appear here — stays on the Incident domain entity only.
/// </summary>
public record AttendanceRecorded : IntegrationEvent
{
    /// <summary>ULID of the AttendanceRecord aggregate (26 chars).</summary>
    public string RecordId  { get; init; } = default!;

    /// <summary>ULID of the student (26 chars).</summary>
    public string StudentId { get; init; } = default!;

    /// <summary>ISO date string "yyyy-MM-dd" — DateOnly serialised at publish boundary (ADR-074).</summary>
    public string Date      { get; init; } = default!;

    /// <summary>Enum name string: "Present", "Absent", or "Late".</summary>
    public string Status    { get; init; } = default!;
}
