using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Notifications.Application.Abstractions;
using Notifications.Domain.Notifications;

namespace Notifications.Application.Notifications.RegisterNotification;

/// <summary>
/// Handler for RegisterNotificationCommand.
/// Simulates delivery of a notification and records the outcome:
///   - Valid channel + non-empty recipient → Notification (Sent) + publish NotificationSent
///   - Invalid channel OR empty recipient   → Notification (Failed) + publish NotificationFailed
///
/// In both cases a row is persisted and an integration event is published through the outbox.
/// The UnitOfWorkBehavior commits the Notification INSERT and the OutboxMessage INSERT atomically
/// AFTER this handler returns. Always returns Result.Success so the consumer ACKs the message
/// (a simulated delivery failure is a recorded business outcome, NOT a transport error).
/// </summary>
public sealed class RegisterNotificationCommandHandler(
    INotificationRepository repo,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<RegisterNotificationCommand, Result<RegisterNotificationResponse>>
{
    public async Task<Result<RegisterNotificationResponse>> Handle(
        RegisterNotificationCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var id = NotificationId.New(now);

        var channelResult = NotificationChannelExtensions.TryCreate(command.Channel);

        // Simulated delivery: fails if the channel is invalid or the recipient is missing.
        var deliveryError =
            !channelResult.IsSuccess ? $"Invalid channel '{command.Channel}'."
            : string.IsNullOrWhiteSpace(command.Recipient) ? "Recipient is empty."
            : null;

        if (deliveryError is not null)
        {
            var channel = channelResult.IsSuccess ? channelResult.Value : NotificationChannel.Email;

            var failed = Notification.CreateFailed(
                id, command.SourceEvent, command.StudentId, channel,
                command.Recipient ?? string.Empty, command.Subject, command.Body,
                deliveryError, now.UtcDateTime);

            await repo.AddAsync(failed, cancellationToken);

            await publishEndpoint.Publish(new NotificationFailed
            {
                NotificationId = id.Value,
                SourceEvent = command.SourceEvent,
                Channel = channel.ToString(),
                Recipient = command.Recipient ?? string.Empty,
                Reason = deliveryError,
                CorrelationId = command.CorrelationId
            }, cancellationToken);

            return Result<RegisterNotificationResponse>.Success(
                new RegisterNotificationResponse(id.Value, NotificationStatus.Failed.ToString()));
        }

        var sentResult = Notification.CreateSent(
            id, command.SourceEvent, command.StudentId, channelResult.Value,
            command.Recipient, command.Subject, command.Body, now.UtcDateTime);

        if (!sentResult.IsSuccess)
        {
            // Domain rejected required fields → record a failed notification instead.
            var failed = Notification.CreateFailed(
                id, command.SourceEvent, command.StudentId, channelResult.Value,
                command.Recipient, command.Subject, command.Body,
                sentResult.Error.Message, now.UtcDateTime);

            await repo.AddAsync(failed, cancellationToken);

            await publishEndpoint.Publish(new NotificationFailed
            {
                NotificationId = id.Value,
                SourceEvent = command.SourceEvent,
                Channel = channelResult.Value.ToString(),
                Recipient = command.Recipient,
                Reason = sentResult.Error.Message,
                CorrelationId = command.CorrelationId
            }, cancellationToken);

            return Result<RegisterNotificationResponse>.Success(
                new RegisterNotificationResponse(id.Value, NotificationStatus.Failed.ToString()));
        }

        await repo.AddAsync(sentResult.Value, cancellationToken);

        await publishEndpoint.Publish(new NotificationSent
        {
            NotificationId = id.Value,
            SourceEvent = command.SourceEvent,
            Channel = channelResult.Value.ToString(),
            Recipient = command.Recipient,
            CorrelationId = command.CorrelationId
        }, cancellationToken);

        return Result<RegisterNotificationResponse>.Success(
            new RegisterNotificationResponse(id.Value, NotificationStatus.Sent.ToString()));
    }
}
