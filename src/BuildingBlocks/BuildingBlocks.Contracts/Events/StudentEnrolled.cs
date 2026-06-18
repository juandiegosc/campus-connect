namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Published when a student is enrolled in the Academic service.
/// Contract frozen after Phase 1 (ADR-034) — EnrollmentId added for Payments trazability.
/// DO NOT add PII fields (DocumentId, GuardianName) — no consumer needs them.
/// </summary>
public record StudentEnrolled : IntegrationEvent
{
    public string StudentId    { get; init; } = default!;
    public string EnrollmentId { get; init; } = default!;  // ADR-034 delta vs docs/02 §7
    public string SchoolId     { get; init; } = default!;
    public string Grade        { get; init; } = default!;
    public string FullName     { get; init; } = default!;
}
