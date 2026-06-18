namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Published by the Payments service when a payment obligation is confirmed.
/// Contract frozen after Academic Phase 1. Consumed by PaymentConfirmedConsumer in Phase 2.
/// </summary>
public record PaymentConfirmed : IntegrationEvent
{
    public string  PaymentId    { get; init; } = default!;
    public string  ObligationId { get; init; } = default!;
    public string  StudentId    { get; init; } = default!;
    public decimal Amount       { get; init; }
    public string  Method       { get; init; } = default!;
}
