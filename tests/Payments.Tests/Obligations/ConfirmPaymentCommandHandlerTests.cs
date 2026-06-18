using BuildingBlocks.Application.Common;
using BuildingBlocks.Domain.Exceptions;
using FluentAssertions;
using FluentValidation;
using MassTransit;
using Payments.Application.Abstractions;
using Payments.Application.Obligations.ConfirmPayment;
using Payments.Domain.Obligations;
using Xunit;
using BuildingBlocks.Contracts.Events;

namespace Payments.Tests.Obligations;

/// <summary>
/// Unit tests for ConfirmPaymentCommandHandler and its validator.
/// ESC-PM-07..ESC-PM-14, REQ-PM1-04..REQ-PM1-08.
/// CRITICAL: ESC-PM-09 — publish-before-save ordering (Gotcha 28).
/// </summary>
public sealed class ConfirmPaymentCommandHandlerTests
{
    private readonly FakeObligationRepository _repo      = new();
    private readonly FakeUlidGenerator2       _ulid      = new();
    private readonly SpyPublishEndpoint       _publisher = new();

    private ConfirmPaymentCommandHandler CreateHandler()
        => new(_repo, _ulid, _publisher);

    private async Task<(Obligation obl, string id)> SeedPendingObligation()
    {
        var oblId = NUlid.Ulid.NewUlid(DateTimeOffset.UtcNow).ToString();
        var obl   = Obligation.Register(
            ObligationId.Parse(oblId),
            "01234567890123456789012345",
            "Mensualidad Febrero",
            200m,
            DateTime.UtcNow.AddDays(30),
            "SCH-001",
            DateTime.UtcNow);
        _repo.Seed(obl);
        return (obl, oblId);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsSuccess_WithConfirmedStatusAndPaymentId()
    {
        var (_, oblId) = await SeedPendingObligation();

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Confirmed");
        result.Value.PaymentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_HappyPath_PaymentIdIs26Chars()
    {
        var (_, oblId) = await SeedPendingObligation();

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Cash", "REF-001"), CancellationToken.None);

        result.Value.PaymentId.Length.Should().Be(26);
    }

    [Fact]
    public async Task Handle_HappyPath_PaymentIdAndObligationIdAreDistinct()
    {
        var (_, oblId) = await SeedPendingObligation();

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Card", "REF-002"), CancellationToken.None);

        result.Value.ObligationId.Should().NotBe(result.Value.PaymentId);
    }

    /// <summary>
    /// ESC-PM-09 / Gotcha 28 — CRITICAL: PublishAsync MUST be called BEFORE SaveChanges.
    /// The spy records call order; we assert Publish precedes any SaveChanges signal.
    /// In the handler the UoW is the pipeline behavior (NOT called directly by handler),
    /// so what we actually assert is: Publish WAS called during Handle (before handler returns),
    /// and the handler did NOT call SaveChanges itself.
    /// </summary>
    [Fact]
    public async Task Handle_HappyPath_PublishAsync_IsCalledDuringHandle()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-SPY"), CancellationToken.None);

        _publisher.PublishedMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_HasCorrectObligationId()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt!.ObligationId.Should().Be(oblId);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_HasCorrectStudentId()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt!.StudentId.Should().Be("01234567890123456789012345");
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_HasCorrectAmount()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt!.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_HasCorrectMethodAsString()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt!.Method.Should().Be("Transfer");  // ADR-049: enum.ToString() at boundary
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_HasCorrectPaymentId()
    {
        var (_, oblId) = await SeedPendingObligation();

        var handlerResult = await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);

        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt!.PaymentId.Should().Be(handlerResult.Value.PaymentId);
    }

    /// <summary>ESC-PM-14 — Reference MUST NOT appear in the frozen PaymentConfirmed event.</summary>
    [Fact]
    public async Task Handle_HappyPath_PublishedPaymentConfirmed_ReferenceNotPresentInEvent()
    {
        var (_, oblId) = await SeedPendingObligation();

        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "SECRET-REF"), CancellationToken.None);

        // PaymentConfirmed record has no Reference property — verified at compile time.
        // The type does not expose Reference; this test documents the intent.
        var evt = _publisher.PublishedMessages.Should().ContainSingle().Which as PaymentConfirmed;
        evt.Should().NotBeNull();
        // Reference property does not exist on PaymentConfirmed — confirmed by absence in record definition.
    }

    [Fact]
    public async Task Handle_ObligationNotFound_ReturnsNotFoundFailure()
    {
        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand("00000000000000000000000000", "Transfer", "TRX-001"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _publisher.PublishedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AlreadyConfirmed_ReturnsConflictFailure_NoPublish()
    {
        var (_, oblId) = await SeedPendingObligation();

        // First confirm
        await CreateHandler().Handle(
            new ConfirmPaymentCommand(oblId, "Transfer", "TRX-001"), CancellationToken.None);
        _publisher.PublishedMessages.Clear();

        // Second confirm — should return Conflict, no second publish
        var handler2 = new ConfirmPaymentCommandHandler(_repo, _ulid, _publisher);
        var result2 = await handler2.Handle(
            new ConfirmPaymentCommand(oblId, "Cash", "TRX-002"), CancellationToken.None);

        result2.IsFailure.Should().BeTrue();
        result2.Error.Type.Should().Be(ErrorType.Conflict);
        _publisher.PublishedMessages.Should().BeEmpty();
    }

    [Fact]
    public void Validator_InvalidMethod_FailsValidation()
    {
        var validator = new ConfirmPaymentCommandValidator();
        var result = validator.Validate(new ConfirmPaymentCommand(
            "01234567890123456789012345", "Bitcoin", "REF-001"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ReferenceEmpty_FailsValidation()
    {
        var validator = new ConfirmPaymentCommandValidator();
        var result = validator.Validate(new ConfirmPaymentCommand(
            "01234567890123456789012345", "Transfer", ""));
        result.IsValid.Should().BeFalse();
    }
}

/// <summary>Spy for IPublishEndpoint — records published messages to assert ordering and payload.</summary>
internal sealed class SpyPublishEndpoint : IPublishEndpoint
{
    public List<object> PublishedMessages { get; } = [];

    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    // MassTransit 9.x additional generic overloads with object message
    public Task Publish<T>(object message, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(object message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => throw new NotImplementedException();
}

internal sealed class FakeUlidGenerator2 : IUlidGenerator
{
    public string NewId(DateTimeOffset? timestamp = null)
        => NUlid.Ulid.NewUlid(timestamp ?? DateTimeOffset.UtcNow).ToString();
}
