using BuildingBlocks.Application.Messaging;

namespace Payments.Application.Obligations.ConfirmPayment;

/// <summary>
/// Command to confirm payment of an Obligation and publish PaymentConfirmed to outbox.
/// ICommand&lt;T&gt; marker ensures UnitOfWorkBehavior activates (Gotcha 16).
/// ICommand&lt;ConfirmPaymentResponse&gt; = IRequest&lt;Result&lt;ConfirmPaymentResponse&gt;&gt; via kernel.
/// </summary>
public sealed record ConfirmPaymentCommand(
    string ObligationId,
    string Method,
    string Reference
) : ICommand<ConfirmPaymentResponse>;
