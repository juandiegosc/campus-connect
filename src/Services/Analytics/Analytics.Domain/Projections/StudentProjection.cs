namespace Analytics.Domain.Projections;

/// <summary>
/// CQRS read-model projection of a student, assembled from StudentEnrolled and StudentStatusUpdated
/// events. Used to compute the "total students" and "pending payments" dashboard metrics.
/// Plain projection model — no domain invariants.
/// </summary>
public sealed class StudentProjection
{
    /// <summary>Primary key — ULID string from the StudentEnrolled event.</summary>
    public string StudentId { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string Grade { get; set; } = default!;

    /// <summary>Academic status, e.g. "Active". Default on enrollment.</summary>
    public string AcademicStatus { get; set; } = "Active";

    /// <summary>Financial status, e.g. "Pending" / "Paid". Starts "Pending" on enrollment.</summary>
    public string FinancialStatus { get; set; } = "Pending";

    public DateTime LastUpdatedAt { get; set; }
}
