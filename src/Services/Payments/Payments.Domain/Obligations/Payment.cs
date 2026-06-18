namespace Payments.Domain.Obligations;

/// <summary>
/// Owned entity embedded in <see cref="Obligation"/>. Created when a payment is confirmed.
/// Reference is stored locally — NEVER appears in the PaymentConfirmed integration event (ADR-044).
/// </summary>
public sealed class Payment
{
    public PaymentId    Id          { get; }
    public PaymentMethod Method     { get; }
    public string       Reference   { get; }   // local only — NOT published (ADR-044)
    public DateTime     ConfirmedAt { get; }

    internal Payment(PaymentId id, PaymentMethod method, string reference, DateTime confirmedAt)
    {
        Id          = id;
        Method      = method;
        Reference   = reference;
        ConfirmedAt = confirmedAt;
    }

    // EF Core parameterless constructor
    private Payment() { Id = null!; Reference = null!; }
}
