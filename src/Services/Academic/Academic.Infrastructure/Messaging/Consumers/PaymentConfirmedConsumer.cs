using Academic.Application.Students.ConfirmPayment;
using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Academic.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer adapter. Thin bridge: Infrastructure → Application.
/// Contains NO business logic — all logic lives in ConfirmStudentPaymentCommandHandler.
/// ADR-039: Consumer-as-Adapter pattern.
/// ADR-042: Must be registered in BOTH DependencyInjection.cs AND AcademicWebApplicationFactory.
/// ADR-043: CorrelationId null fallback — log warning + use MassTransit transport CorrelationId.
/// </summary>
public sealed class PaymentConfirmedConsumer : IConsumer<PaymentConfirmed>
{
    private readonly ISender                              _sender;
    private readonly ILogger<PaymentConfirmedConsumer>   _logger;

    public PaymentConfirmedConsumer(
        ISender sender,
        ILogger<PaymentConfirmedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentConfirmed> context)
    {
        var msg = context.Message;

        // ADR-043: CorrelationId null fallback — do NOT fault on a missing trace field
        var correlationId = msg.CorrelationId;
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = FallbackCorrelationId(context, msg.StudentId, msg.PaymentId);
        }

        var result = await _sender.Send(
            new ConfirmStudentPaymentCommand(msg.StudentId, msg.PaymentId, correlationId),
            context.CancellationToken);

        if (result.IsFailure)
        {
            // Throw → MassTransit triggers retry policy → after exhaustion → _error queue (ADR-041)
            // NotFound errors are terminal (no retry will fix missing student data).
            _logger.LogError(
                "ConfirmStudentPayment failed for StudentId={StudentId} CorrelationId={CorrelationId}: {ErrorCode} - {ErrorMessage}",
                msg.StudentId, correlationId, result.Error.Code, result.Error.Message);

            throw new InvalidOperationException(
                $"ConfirmStudentPayment failed: {result.Error.Code} - {result.Error.Message}");
        }
    }

    private string FallbackCorrelationId(
        ConsumeContext context, string studentId, string paymentId)
    {
        _logger.LogWarning(
            "PaymentConfirmed received with null/empty CorrelationId. " +
            "Falling back to MassTransit transport CorrelationId. " +
            "StudentId={StudentId} PaymentId={PaymentId} TransportCorrelationId={TransportCorrelationId}",
            studentId, paymentId, context.CorrelationId);

        return context.CorrelationId?.ToString() ?? string.Empty;
    }
}
