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
    /// Academic lifecycle status (Active|Suspended|Graduated), stored verbatim as the
    /// Academic enum name (ADR-061). Nullable — set only after a StudentStatusUpdated event
    /// (Phase 3); rows created by StudentEnrolled before any status event remain null.
    /// </summary>
    public string? AcademicStatus  { get; set; }

    /// <summary>
    /// Financial status (Pending|Paid|Overdue), stored verbatim (ADR-061). Nullable — see AcademicStatus.
    /// </summary>
    public string? FinancialStatus { get; set; }

    /// <summary>
    /// Set on every upsert (consumer clock). Doubles as a deterministic ORDER BY.
    /// Exposed in the DTO as the list-view timestamp (ADR-059 R8).
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
}
