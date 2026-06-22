using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Notifications.Application.Notifications.RegisterNotification;

namespace Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Pub/Sub consumer: reacts to IncidentReported by sending an incident alert notification.
/// </summary>
public sealed class IncidentReportedConsumer(
    ISender sender,
    ILogger<IncidentReportedConsumer> logger) : IConsumer<IncidentReported>
{
    public async Task Consume(ConsumeContext<IncidentReported> context)
    {
        var msg = context.Message;
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "IncidentReported received with null/empty CorrelationId. Falling back to transport CorrelationId. StudentId={StudentId}",
                msg.StudentId);
            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        var result = await sender.Send(new RegisterNotificationCommand(
            SourceEvent: nameof(IncidentReported),
            StudentId: msg.StudentId,
            Channel: "Email",
            Recipient: $"acudiente-{msg.StudentId}@campusconnect.edu",
            Subject: $"Incidente reportado ({msg.Severity})",
            Body: $"Se reportó un incidente de tipo '{msg.Type}' con severidad {msg.Severity}.",
            CorrelationId: correlationId), context.CancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"RegisterNotification failed for IncidentReported: {result.Error.Code} - {result.Error.Message}");
    }
}
