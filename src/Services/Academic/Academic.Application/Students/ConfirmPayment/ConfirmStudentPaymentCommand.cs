using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.ConfirmPayment;

/// <summary>
/// Internal command — dispatched by PaymentConfirmedConsumer (Infrastructure).
/// No FluentValidation rules needed: StudentId and CorrelationId are pre-validated by the
/// consumer adapter before dispatch.
///
/// CRITICAL: implements ICommand (not IRequest&lt;T&gt;) to activate UnitOfWorkBehavior (Gotcha 16).
/// </summary>
public sealed record ConfirmStudentPaymentCommand(
    string StudentId,
    string PaymentId,
    string CorrelationId
) : ICommand;
