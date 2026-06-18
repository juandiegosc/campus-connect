namespace Payments.Application.Obligations.GetObligationById;

public sealed record PaymentDto(
    string   PaymentId,
    string   Method,
    string   Reference,    // local only — NEVER in the published event (ADR-044)
    DateTime ConfirmedAt);
