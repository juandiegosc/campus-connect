using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Notifications.Application.Notifications.RegisterNotification;

namespace Notifications.Infrastructure.Messaging.Consumers;

/// <summary>
/// Pub/Sub consumer: reacts to StudentEnrolled by sending a welcome notification.
/// Thin adapter (ADR-039): delegates to RegisterNotificationCommand via MediatR.
/// </summary>
public sealed class StudentEnrolledConsumer(
    ISender sender,
    ILogger<StudentEnrolledConsumer> logger) : IConsumer<StudentEnrolled>
{
    public async Task Consume(ConsumeContext<StudentEnrolled> context)
    {
        var msg = context.Message;
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning(
                "StudentEnrolled received with null/empty CorrelationId. Falling back to transport CorrelationId. StudentId={StudentId}",
                msg.StudentId);
            correlationId = context.CorrelationId?.ToString() ?? string.Empty;
        }

        var result = await sender.Send(new RegisterNotificationCommand(
            SourceEvent: nameof(StudentEnrolled),
            StudentId: msg.StudentId,
            Channel: "Email",
            Recipient: $"acudiente-{msg.StudentId}@campusconnect.edu",
            Subject: "Bienvenido a CampusConnect 360",
            Body: $"El estudiante {msg.FullName} ha sido matriculado en el grado {msg.Grade}.",
            CorrelationId: correlationId), context.CancellationToken);

        if (result.IsFailure)
            throw new InvalidOperationException(
                $"RegisterNotification failed for StudentEnrolled: {result.Error.Code} - {result.Error.Message}");
    }
}
