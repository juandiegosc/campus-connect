using BuildingBlocks.Domain.Events;

namespace Payments.Domain.Obligations.Events;

/// <summary>
/// Internal domain event raised when an Obligation transitions from Pending to Confirmed.
/// Distinct from the BuildingBlocks.Contracts PaymentConfirmed integration event.
/// Method is already a string (enum.ToString() applied at handler boundary — ADR-049).
/// </summary>
public sealed record PaymentConfirmedDomainEvent(
    string  PaymentId,
    string  ObligationId,
    string  StudentId,
    decimal Amount,
    string  Method
) : IDomainEvent;
