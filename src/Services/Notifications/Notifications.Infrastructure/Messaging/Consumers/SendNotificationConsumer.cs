using BuildingBlocks.Contracts.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Notifications.Application.Notifications.RegisterNotification;

namespace Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Point-to-Point consumer: processes SendNotificationCommand messages SENT directly to the
/// Notifications queue (queue:notifications-send-notification). Demonstrates the point-to-point
/// pattern — each command is handled by exactly ONE consumer instance.
/// </summary>
public sealed class SendNotificationConsumer(
    ISender sender,
    ILogger<SendNotificationConsumer> logger) : IConsumer<SendNotificationCommand>
{
    public async Task Consume(ConsumeContext<SendNotificationCommand> context)
    {
        var msg = context.Message;
        var correlationId = string.IsNullOrEmpty(msg.CorrelationId)
            ? context.CorrelationId?.ToString() ?? string.Empty
            : msg.CorrelationId;

        logger.LogInformation(
            "SendNotificationCommand received (point-to-point). Recipient={Recipient} Channel={Channel}",
            msg.Recipient, msg.Channel);

        var result = await sender.Send(new RegisterNotificationCommand(
            SourceEvent: nameof(SendNotificationCommand),
            StudentId: null,
            Channel: msg.Channel,
            Recipient: msg.Recipient,
            Subject: msg.Subject,
            Body: msg.Body,
            CorrelationId: correlationId), context.CancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"RegisterNotification failed for SendNotificationCommand: {result.Error.Code} - {result.Error.Message}");
    }
}
