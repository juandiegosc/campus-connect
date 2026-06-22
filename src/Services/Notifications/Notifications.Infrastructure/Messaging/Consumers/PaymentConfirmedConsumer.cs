using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Notifications.Application.Notifications.RegisterNotification;

namespace Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Pub/Sub consumer: reacts to PaymentConfirmed by sending a payment receipt notification.
/// </summary>
public sealed class PaymentConfirmedConsumer(
    ISender sender,
    ILogger<PaymentConfirmedConsumer> logger) : IConsumer<PaymentConfirmed>
{
    public async Task Consume(ConsumeContext<PaymentConfirmed> context)
    {
        var msg = context.Message;
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "PaymentConfirmed received with null/empty CorrelationId. Falling back to transport CorrelationId. StudentId={StudentId}",
                msg.StudentId);
            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        var result = await sender.Send(new RegisterNotificationCommand(
            SourceEvent: nameof(PaymentConfirmed),
            StudentId: msg.StudentId,
            Channel: "Email",
            Recipient: $"acudiente-{msg.StudentId}@campusconnect.edu",
            Subject: "Pago confirmado",
            Body: $"Se confirmó el pago {msg.PaymentId} por {msg.Amount:0.00} ({msg.Method}).",
            CorrelationId: correlationId), context.CancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"RegisterNotification failed for PaymentConfirmed: {result.Error.Code} - {result.Error.Message}");
    }
}
