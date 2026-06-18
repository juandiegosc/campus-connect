using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Primitives;
using Payments.Domain.Obligations.Events;

namespace Payments.Domain.Obligations;

/// <summary>
/// Obligation aggregate root. Owns an embedded Payment entity (1:1, null until confirmed).
/// SchoolId hardcoded "SCH-001" — // TODO multi-tenant.
/// Domain stays free of Application Result&lt;T&gt; (Gotcha 24).
/// </summary>
public sealed class Obligation : AggregateRoot<ObligationId>
{
    public string           StudentId   { get; private set; } = default!;   // TRUSTED — 26-char format check (ADR-051)
    public string           Concept     { get; private set; } = default!;
    public decimal          Amount      { get; private set; }                // > 0 (ADR-050)
    public DateTime         DueDate     { get; private set; }
    public string           SchoolId    { get; private set; } = default!;   // TODO multi-tenant
    public ObligationStatus Status      { get; private set; }
    public Payment?         Payment     { get; private set; }
    public DateTime         CreatedAt   { get; private set; }

    // EF Core parameterless constructor
    private Obligation() { }

    /// <summary>
    /// Creates a new Obligation in Pending status with no Payment.
    /// Validates domain invariants; throws DomainException on failure.
    /// Command validator catches soft violations first.
    /// </summary>
    public static Obligation Register(
        ObligationId id,
        string       studentId,
        string       concept,
        decimal      amount,
        DateTime     dueDate,
        string       schoolId,
        DateTime     nowUtc)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Length != 26)
            throw new DomainException($"Invalid StudentId '{studentId}': must be a 26-character ULID string.");

        if (string.IsNullOrWhiteSpace(concept))
            throw new DomainException("Concept is required.");

        if (concept.Length > 200)
            throw new DomainException("Concept must not exceed 200 characters.");

        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");

        if (dueDate == default)
            throw new DomainException("DueDate must be provided.");

        return new Obligation
        {
            Id        = id,
            StudentId = studentId.Trim(),
            Concept   = concept.Trim(),
            Amount    = amount,
            DueDate   = dueDate,
            SchoolId  = schoolId,
            Status    = ObligationStatus.Pending,
            Payment   = null,
            CreatedAt = nowUtc
        };
    }

    /// <summary>
    /// Transitions the obligation from Pending to Confirmed and embeds the Payment entity.
    /// MUST be void — Domain does NOT reference Application Result&lt;T&gt; (Gotcha 24, R7 design).
    /// Handler checks Status == Confirmed BEFORE calling this method and returns Conflict (409).
    /// Defensive DomainException here if called out of order.
    /// </summary>
    public void ConfirmPayment(
        PaymentId     paymentId,
        PaymentMethod method,
        string        reference,
        DateTime      nowUtc)
    {
        if (Status == ObligationStatus.Confirmed)
            throw new DomainException("Obligation is already confirmed. Use handler-level idempotency guard.");

        Payment = new Payment(paymentId, method, reference, nowUtc);
        Status  = ObligationStatus.Confirmed;

        Raise(new PaymentConfirmedDomainEvent(
            paymentId.Value,
            Id.Value,
            StudentId,
            Amount,
            method.ToString()   // enum→string at boundary (ADR-049)
        ));
    }
}
