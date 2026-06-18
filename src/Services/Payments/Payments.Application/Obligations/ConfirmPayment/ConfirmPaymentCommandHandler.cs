using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.ConfirmPayment;

/// <summary>
/// Handles ConfirmPaymentCommand.
/// CRITICAL flow order (Gotcha 28/3 — publish BEFORE SaveChanges):
///   1. Load obligation — 404 if not found
///   2. Idempotency guard: if already Confirmed → 409 Conflict, NO publish
///   3. Parse PaymentMethod enum
///   4. Generate PaymentId + confirmedAt
///   5. obligation.ConfirmPayment(...) [void — domain free of Result&lt;T&gt; (Gotcha 24)]
///   6. repo.Update(obligation) — EF tracks; NO SaveChanges
///   7. ★ BEFORE returning: IPublishEndpoint.Publish&lt;PaymentConfirmed&gt;(evt) → OutboxMessage INSERT
///   8. Return Result.Success
///   UnitOfWorkBehavior commits obligations UPDATE + OutboxMessage INSERT in SAME TX.
/// </summary>
public sealed class ConfirmPaymentCommandHandler(
    IObligationRepository repo,
    IUlidGenerator        ulid,
    IPublishEndpoint      publishEndpoint)
    : IRequestHandler<ConfirmPaymentCommand, Result<ConfirmPaymentResponse>>  // ICommand<TResp> = IRequest<Result<TResp>>
{
    public async Task<Result<ConfirmPaymentResponse>> Handle(
        ConfirmPaymentCommand command,
        CancellationToken     cancellationToken)
    {
        // 1. Load obligation
        ObligationId oblId;
        try { oblId = ObligationId.Parse(command.ObligationId); }
        catch { return Result<ConfirmPaymentResponse>.Failure(Error.NotFound("obligation.not_found", $"Obligation '{command.ObligationId}' not found.")); }

        var obligation = await repo.GetByIdAsync(oblId, cancellationToken);
        if (obligation is null)
            return Result<ConfirmPaymentResponse>.Failure(Error.NotFound("obligation.not_found", $"Obligation '{command.ObligationId}' not found."));

        // 2. Idempotency guard (handler-level — domain stays free of Result<T>)
        if (obligation.Status == ObligationStatus.Confirmed)
            return Result<ConfirmPaymentResponse>.Failure(Error.Conflict(
                "obligation.already_confirmed",
                "Obligation already has a confirmed payment."));

        // 3. Parse method
        if (!Enum.TryParse<PaymentMethod>(command.Method, ignoreCase: true, out var method))
            return Result<ConfirmPaymentResponse>.Failure(Error.Validation(
                "payment_method.invalid",
                $"Method '{command.Method}' is not valid. Must be one of: {string.Join(", ", Enum.GetNames<PaymentMethod>())}."));

        // 4. Generate ids + timestamp
        var now       = DateTimeOffset.UtcNow;
        var paymentId = PaymentId.Parse(ulid.NewId(now.AddTicks(1)));
        var confirmedAt = now.UtcDateTime;

        // 5. Domain transition (void — defensive DomainException if guard above was bypassed)
        obligation.ConfirmPayment(paymentId, method, command.Reference, confirmedAt);

        // 6. Mark dirty — NO SaveChanges (UoW owns commit)
        repo.Update(obligation);

        // 7. ★ CRITICAL (Gotcha 28): Publish BEFORE returning so UoW commits obligations + OutboxMessage atomically
        await publishEndpoint.Publish(new PaymentConfirmed
        {
            PaymentId    = paymentId.Value,
            ObligationId = obligation.Id.Value,
            StudentId    = obligation.StudentId,
            Amount       = obligation.Amount,
            Method       = method.ToString()     // enum→string at boundary (ADR-049)
            // Reference NOT included — ADR-044, REQ-PM1-08
        }, cancellationToken);

        // 8. Return (UnitOfWorkBehavior saves atomically after this)
        return Result<ConfirmPaymentResponse>.Success(
            new ConfirmPaymentResponse(obligation.Id.Value, "Confirmed", paymentId.Value, confirmedAt));
    }
}
