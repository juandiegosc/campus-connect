namespace Payments.Domain.Obligations;

/// <summary>
/// Accepted payment methods. Stored as string via EF conversion (ADR-049).
/// Mapped to string at the publish boundary via .ToString().
/// </summary>
public enum PaymentMethod
{
    Cash,
    Transfer,
    Card
}
