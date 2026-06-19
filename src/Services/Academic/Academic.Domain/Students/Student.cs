using Academic.Domain.Students.Events;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Primitives;

namespace Academic.Domain.Students;

/// <summary>
/// Student aggregate root. Encapsulates enrollment, academic, and financial status.
/// SchoolId is hardcoded to "SCH-001" — // TODO multi-tenant
/// </summary>
public sealed class Student : AggregateRoot<StudentId>
{
    public string         FullName        { get; private set; } = default!;
    public DocumentId     DocumentId      { get; private set; } = default!;
    public string         Grade           { get; private set; } = default!;
    public string         SchoolId        { get; private set; } = default!;  // TODO multi-tenant
    public GuardianContact Guardian       { get; private set; } = default!;
    public AcademicStatus  AcademicStatus  { get; private set; }
    public FinancialStatus FinancialStatus { get; private set; }
    public Enrollment     Enrollment      { get; private set; } = default!;
    public DateTime       CreatedAt       { get; private set; }

    // EF Core parameterless constructor
    private Student() { }

    /// <summary>
    /// Creates a new Student aggregate with the given parameters.
    /// Validates all invariants and raises <see cref="StudentEnrolledDomainEvent"/>.
    /// </summary>
    public static Student Create(
        StudentId      studentId,
        string         fullName,
        DocumentId     documentId,
        string         grade,
        string         schoolId,
        GuardianContact guardian,
        string         enrollmentId,
        DateTime       nowUtc)
    {
        // Guard invariants — raise NO events on failure
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Student FullName is required.");

        if (fullName.Length > 120)
            throw new DomainException("Student FullName must not exceed 120 characters.");

        if (string.IsNullOrWhiteSpace(grade))
            throw new DomainException("Student Grade is required.");

        var student = new Student
        {
            Id              = studentId,
            FullName        = fullName.Trim(),
            DocumentId      = documentId,
            Grade           = grade.Trim(),
            SchoolId        = schoolId,
            Guardian        = guardian,
            AcademicStatus  = AcademicStatus.Active,
            FinancialStatus = FinancialStatus.Pending,
            Enrollment      = new Enrollment(enrollmentId, nowUtc),
            CreatedAt       = nowUtc
        };

        student.Raise(new StudentEnrolledDomainEvent(
            studentId.Value,
            enrollmentId,
            schoolId,
            grade,
            fullName));

        return student;
    }

    /// <summary>
    /// Transitions FinancialStatus from Pending or Overdue to Paid.
    /// Idempotent: if already Paid, no event is raised.
    /// </summary>
    public void ConfirmPayment(DateTime nowUtc)
    {
        if (FinancialStatus == FinancialStatus.Paid)
            return; // idempotent — no-op

        var oldStatus = FinancialStatus;
        FinancialStatus = FinancialStatus.Paid;

        Raise(new StudentFinancialStatusChangedDomainEvent(
            Id.Value,
            oldStatus.ToString(),
            FinancialStatus.ToString(),
            nowUtc));
    }

    /// <summary>
    /// Transitions FinancialStatus to Overdue (Phase 3 — ADR-063).
    /// Pending → Overdue (raises <see cref="StudentFinancialStatusChangedDomainEvent"/>).
    /// Overdue → idempotent no-op (no event). Paid → invalid transition (DomainException);
    /// the application handler guards this case and returns 409 Conflict before reaching here.
    /// </summary>
    public void MarkOverdue(DateTime nowUtc)
    {
        if (FinancialStatus == FinancialStatus.Overdue)
            return; // idempotent — no-op

        if (FinancialStatus == FinancialStatus.Paid)
            throw new DomainException("A student with Paid financial status cannot be marked overdue.");

        var oldStatus = FinancialStatus; // Pending
        FinancialStatus = FinancialStatus.Overdue;

        Raise(new StudentFinancialStatusChangedDomainEvent(
            Id.Value,
            oldStatus.ToString(),
            FinancialStatus.ToString(),
            nowUtc));
    }

    /// <summary>
    /// Transitions AcademicStatus to Suspended (Phase 4 — ADR-068).
    /// Active → Suspended. Suspended → idempotent no-op (no event).
    /// Graduated → invalid transition (DomainException); handler guards → 409 before reaching here.
    /// Raises NO domain event (ADR-068 — deliberate asymmetry with financial transitions).
    /// </summary>
    public void Suspend(DateTime nowUtc)
    {
        if (AcademicStatus == AcademicStatus.Suspended)
            return;           // idempotent no-op

        if (AcademicStatus == AcademicStatus.Graduated)
            throw new DomainException("A graduated student cannot be suspended.");

        AcademicStatus = AcademicStatus.Suspended;  // Active → Suspended
    }

    /// <summary>
    /// Transitions AcademicStatus to Active (Phase 4 — ADR-068).
    /// Suspended → Active. Active → idempotent no-op (no event).
    /// Graduated → invalid transition (DomainException); handler guards → 409 before reaching here.
    /// Raises NO domain event (ADR-068).
    /// </summary>
    public void Reactivate(DateTime nowUtc)
    {
        if (AcademicStatus == AcademicStatus.Active)
            return;           // idempotent no-op

        if (AcademicStatus == AcademicStatus.Graduated)
            throw new DomainException("A graduated student cannot be reactivated.");

        AcademicStatus = AcademicStatus.Active;     // Suspended → Active
    }

    /// <summary>
    /// Transitions AcademicStatus to Graduated (Phase 4 — ADR-066, ADR-068).
    /// Active | Suspended → Graduated (TERMINAL — re-graduating throws DomainException, ADR-066).
    /// The handler guards already-Graduated → 409 Conflict before reaching here.
    /// Raises NO domain event (ADR-068).
    /// </summary>
    public void Graduate(DateTime nowUtc)
    {
        if (AcademicStatus == AcademicStatus.Graduated)
            throw new DomainException("Student is already graduated.");  // TERMINAL — NOT a no-op (ADR-066)

        AcademicStatus = AcademicStatus.Graduated;  // Active | Suspended → Graduated
    }
}
