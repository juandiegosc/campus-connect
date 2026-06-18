namespace Payments.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Foreign-context projection (Academic owns Student identity).
/// NOT a Domain aggregate — no base class, no domain events, no invariants.
/// Plain mutable persistence model (ADR-054).
/// Lives in Infrastructure; never exposed across layer boundaries.
/// </summary>
public sealed class StudentReplica
{
    /// <summary>Primary key — ULID string from the StudentEnrolled event.</summary>
    public string StudentId      { get; set; } = default!;

    public string FullName       { get; set; } = default!;
    public string Grade          { get; set; } = default!;
    public string SchoolId       { get; set; } = default!;

    /// <summary>
    /// Set on every upsert (consumer clock). Doubles as a deterministic ORDER BY.
    /// Exposed in the DTO as the list-view timestamp (ADR-059 R8).
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
}
