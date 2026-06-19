namespace Attendance.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Foreign-context projection read model (REQ-AT1-16, ADR-054).
/// NOT a Domain aggregate — no base class, no domain events, no invariants.
/// Plain mutable persistence model. Lives in Infrastructure only.
/// Fed by StudentEnrolledConsumer upsert.
/// </summary>
public sealed class StudentReplica
{
    /// <summary>Primary key — ULID string from the StudentEnrolled event.</summary>
    public string StudentId     { get; set; } = default!;

    public string FullName      { get; set; } = default!;
    public string Grade         { get; set; } = default!;
    public string SchoolId      { get; set; } = default!;

    /// <summary>Set on every upsert (consumer clock).</summary>
    public DateTime LastUpdatedAt { get; set; }
}
