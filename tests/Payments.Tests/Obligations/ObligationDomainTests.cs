using BuildingBlocks.Domain.Exceptions;
using FluentAssertions;
using Payments.Domain.Obligations;
using Payments.Domain.Obligations.Events;
using Xunit;

namespace Payments.Tests.Obligations;

/// <summary>
/// Unit tests for domain types: ULID VOs, enums, Obligation aggregate.
/// Covers ESC-PM-21..ESC-PM-25, REQ-PM1-11, REQ-PM1-12.
/// TDD: tests written FIRST (RED) before implementations.
/// </summary>
public sealed class ObligationDomainTests
{
    // ── ObligationId / PaymentId ──────────────────────────────────────────────

    [Fact]
    public void ObligationId_Parse_Valid26CharString_Succeeds()
    {
        var id = ObligationId.New(DateTimeOffset.UtcNow);
        var parsed = ObligationId.Parse(id.Value);
        parsed.Value.Should().Be(id.Value);
    }

    [Fact]
    public void ObligationId_Parse_InvalidString_ThrowsDomainException()
    {
        var act = () => ObligationId.Parse("too-short");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PaymentId_Parse_Valid26CharString_Succeeds()
    {
        var id = PaymentId.New(DateTimeOffset.UtcNow);
        var parsed = PaymentId.Parse(id.Value);
        parsed.Value.Should().Be(id.Value);
    }

    [Fact]
    public void PaymentId_Parse_InvalidString_ThrowsDomainException()
    {
        var act = () => PaymentId.Parse("bad");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ObligationId_And_PaymentId_AreDistinctTypes()
    {
        // Compile-time distinct — assignment from one to other should not compile.
        // At runtime we assert the two IDs from a confirmed obligation are different values (ESC-PM-25).
        var oblId = ObligationId.New(DateTimeOffset.UtcNow);
        var payId = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));
        oblId.Value.Should().NotBe(payId.Value); // different values (different timestamps)
    }

    [Fact]
    public void ObligationStatus_HasExactlyTwoValues_PendingAndConfirmed()
    {
        var values = Enum.GetValues<ObligationStatus>();
        values.Should().HaveCount(2);
        values.Should().Contain(ObligationStatus.Pending);
        values.Should().Contain(ObligationStatus.Confirmed);
    }

    [Fact]
    public void PaymentMethod_HasExactlyThreeValues_CashTransferCard()
    {
        var values = Enum.GetValues<PaymentMethod>();
        values.Should().HaveCount(3);
        values.Should().Contain(PaymentMethod.Cash);
        values.Should().Contain(PaymentMethod.Transfer);
        values.Should().Contain(PaymentMethod.Card);
    }

    // ── Obligation aggregate ──────────────────────────────────────────────────

    private static (ObligationId, string, string, decimal, DateTime) ValidArgs()
    {
        var id = ObligationId.New(DateTimeOffset.UtcNow);
        return (id, "01234567890123456789012345", "Mensualidad Enero", 100m, DateTime.UtcNow.AddDays(30));
    }

    [Fact]
    public void Obligation_Register_HappyPath_StatusIsPending_PaymentIsNull()
    {
        var (id, studentId, concept, amount, dueDate) = ValidArgs();

        var obl = Obligation.Register(id, studentId, concept, amount, dueDate, "SCH-001", DateTime.UtcNow);

        obl.Status.Should().Be(ObligationStatus.Pending);
        obl.Payment.Should().BeNull();
        obl.Id.Value.Should().Be(id.Value);
        obl.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Obligation_Register_AmountZero_FailsOrThrows()
    {
        var (id, studentId, concept, _, dueDate) = ValidArgs();

        var act = () => Obligation.Register(id, studentId, concept, 0m, dueDate, "SCH-001", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_Register_AmountNegative_FailsOrThrows()
    {
        var (id, studentId, concept, _, dueDate) = ValidArgs();

        var act = () => Obligation.Register(id, studentId, concept, -1m, dueDate, "SCH-001", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_Register_ConceptEmpty_FailsOrThrows()
    {
        var (id, studentId, _, amount, dueDate) = ValidArgs();

        var act = () => Obligation.Register(id, studentId, "", amount, dueDate, "SCH-001", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_Register_InvalidStudentIdFormat_FailsOrThrows()
    {
        var (id, _, concept, amount, dueDate) = ValidArgs();

        var act = () => Obligation.Register(id, "too-short", concept, amount, dueDate, "SCH-001", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_Register_DueDateDefault_FailsOrThrows()
    {
        var (id, studentId, concept, amount, _) = ValidArgs();

        var act = () => Obligation.Register(id, studentId, concept, amount, default, "SCH-001", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_ConfirmPayment_PendingToConfirmed_CreatesPayment()
    {
        var (id, studentId, concept, amount, dueDate) = ValidArgs();
        var obl = Obligation.Register(id, studentId, concept, amount, dueDate, "SCH-001", DateTime.UtcNow);
        var payId = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));

        obl.ConfirmPayment(payId, PaymentMethod.Transfer, "TRX-001", DateTime.UtcNow);

        obl.Status.Should().Be(ObligationStatus.Confirmed);
        obl.Payment.Should().NotBeNull();
        obl.Payment!.Id.Value.Should().Be(payId.Value);
        obl.Payment.Method.Should().Be(PaymentMethod.Transfer);
        obl.Payment.Reference.Should().Be("TRX-001");
    }

    [Fact]
    public void Obligation_ConfirmPayment_RaisesPaymentConfirmedDomainEvent()
    {
        var (id, studentId, concept, amount, dueDate) = ValidArgs();
        var obl = Obligation.Register(id, studentId, concept, amount, dueDate, "SCH-001", DateTime.UtcNow);
        obl.ClearDomainEvents(); // clear any Register events

        var payId = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));
        obl.ConfirmPayment(payId, PaymentMethod.Cash, "REF-001", DateTime.UtcNow);

        obl.DomainEvents.Should().ContainSingle(e => e is PaymentConfirmedDomainEvent);
    }

    [Fact]
    public void Obligation_ConfirmPayment_AlreadyConfirmed_ThrowsDomainException()
    {
        var (id, studentId, concept, amount, dueDate) = ValidArgs();
        var obl = Obligation.Register(id, studentId, concept, amount, dueDate, "SCH-001", DateTime.UtcNow);
        var payId1 = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));
        obl.ConfirmPayment(payId1, PaymentMethod.Transfer, "TRX-001", DateTime.UtcNow);

        // Second confirm should throw defensively (handler guard is first, but domain also protects)
        var payId2 = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(2));
        var act = () => obl.ConfirmPayment(payId2, PaymentMethod.Cash, "REF-002", DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Obligation_ConfirmPayment_ObligationIdAndPaymentId_AreDistinct()
    {
        var (id, studentId, concept, amount, dueDate) = ValidArgs();
        var obl = Obligation.Register(id, studentId, concept, amount, dueDate, "SCH-001", DateTime.UtcNow);
        var payId = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));

        obl.ConfirmPayment(payId, PaymentMethod.Card, "TRX-002", DateTime.UtcNow);

        obl.Id.Value.Should().NotBe(obl.Payment!.Id.Value);
    }
}
