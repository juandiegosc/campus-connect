namespace Payments.Application.Obligations.ConfirmPayment;

public sealed record ConfirmPaymentResponse(
    string   ObligationId,
    string   Status,
    string   PaymentId,
    DateTime ConfirmedAt);
