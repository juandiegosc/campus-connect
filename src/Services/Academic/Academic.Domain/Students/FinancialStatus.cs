namespace Academic.Domain.Students;

/// <summary>
/// Represents the financial obligation status of a student.
/// Overdue is declared in Phase 1 but only activated by the Payments service (Phase 2).
/// </summary>
public enum FinancialStatus
{
    Pending,
    Paid,
    Overdue
}
