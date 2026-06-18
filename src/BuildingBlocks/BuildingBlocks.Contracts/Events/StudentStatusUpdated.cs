namespace BuildingBlocks.Contracts.Events;

/// <summary>
/// Published when a student's academic or financial status changes.
/// Contract frozen after Phase 1. Published in Phase 2 (PaymentConfirmedConsumer).
/// </summary>
public record StudentStatusUpdated : IntegrationEvent
{
    public string StudentId       { get; init; } = default!;
    public string AcademicStatus  { get; init; } = default!;
    public string FinancialStatus { get; init; } = default!;
}
