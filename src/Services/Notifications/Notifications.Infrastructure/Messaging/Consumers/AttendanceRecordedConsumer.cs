using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Notifications.Application.Notifications.RegisterNotification;

namespace Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Pub/Sub consumer: reacts to AttendanceRecorded. Sends an alert when the student was
/// Absent or Late; otherwise records an informational notification.
/// </summary>
public sealed class AttendanceRecordedConsumer(
    ISender sender,
    ILogger<AttendanceRecordedConsumer> logger) : IConsumer<AttendanceRecorded>
{
    public async Task Consume(ConsumeContext<AttendanceRecorded> context)
    {
        var msg = context.Message;
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "AttendanceRecorded received with null/empty CorrelationId. Falling back to transport CorrelationId. StudentId={StudentId}",
                msg.StudentId);
            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        var isAlert = msg.Status is "Absent" or "Late";

        var result = await sender.Send(new RegisterNotificationCommand(
            SourceEvent: nameof(AttendanceRecorded),
            StudentId: msg.StudentId,
            Channel: "Email",
            Recipient: $"acudiente-{msg.StudentId}@campusconnect.edu",
            Subject: isAlert ? "Alerta de asistencia" : "Asistencia registrada",
            Body: $"Asistencia del {msg.Date}: {msg.Status}.",
            CorrelationId: correlationId), context.CancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"RegisterNotification failed for AttendanceRecorded: {result.Error.Code} - {result.Error.Message}");
    }
}
