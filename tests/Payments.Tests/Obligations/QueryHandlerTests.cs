using BuildingBlocks.Application.Common;
using FluentAssertions;
using Payments.Application.Abstractions;
using Payments.Application.Obligations.GetObligationById;
using Payments.Application.Obligations.GetObligations;
using Payments.Domain.Obligations;
using Xunit;

namespace Payments.Tests.Obligations;

/// <summary>
/// Unit tests for GetObligationsQueryHandler and GetObligationByIdQueryHandler.
/// ESC-PM-15..ESC-PM-20, REQ-PM1-09, REQ-PM1-10.
/// </summary>
public sealed class QueryHandlerTests
{
    private readonly FakeObligationRepository3 _repo = new();

    private Obligation MakePending(string? oblId = null)
    {
        var id = oblId ?? NUlid.Ulid.NewUlid(DateTimeOffset.UtcNow).ToString();
        return Obligation.Register(
            ObligationId.Parse(id),
            "01234567890123456789012345",
            "Cuota " + id[..4],
            100m,
            DateTime.UtcNow.AddDays(30),
            "SCH-001",
            DateTime.UtcNow);
    }

    private Obligation MakeConfirmed()
    {
        var obl = MakePending();
        var payId = PaymentId.New(DateTimeOffset.UtcNow.AddTicks(1));
        obl.ConfirmPayment(payId, PaymentMethod.Transfer, "TRX-001", DateTime.UtcNow);
        return obl;
    }

    // ── GetObligations ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetObligations_WithPendingFilter_ReturnsOnlyPendingItems()
    {
        _repo.Seed(MakePending()); _repo.Seed(MakePending()); _repo.Seed(MakeConfirmed());
        var handler = new GetObligationsQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationsQuery("Pending"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.All(o => o.Status == "Pending").Should().BeTrue();
    }

    [Fact]
    public async Task GetObligations_WithConfirmedFilter_ReturnsOnlyConfirmedItems()
    {
        _repo.Seed(MakePending()); _repo.Seed(MakeConfirmed()); _repo.Seed(MakeConfirmed());
        var handler = new GetObligationsQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationsQuery("Confirmed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.All(o => o.Status == "Confirmed").Should().BeTrue();
    }

    [Fact]
    public async Task GetObligations_NoFilter_ReturnsAllItems()
    {
        _repo.Seed(MakePending()); _repo.Seed(MakeConfirmed());
        var handler = new GetObligationsQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationsQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetObligations_InvalidStatusString_Returns400()
    {
        var handler = new GetObligationsQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationsQuery("Unknown"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    // ── GetObligationById ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetObligationById_ExistingPendingId_ReturnsDetailDtoWithNullPayment()
    {
        var obl = MakePending();
        _repo.Seed(obl);
        var handler = new GetObligationByIdQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationByIdQuery(obl.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        result.Value.Payment.Should().BeNull();
    }

    [Fact]
    public async Task GetObligationById_ExistingConfirmedId_ReturnsDetailDtoWithPayment()
    {
        var obl = MakeConfirmed();
        _repo.Seed(obl);
        var handler = new GetObligationByIdQueryHandler(_repo);

        var result = await handler.Handle(new GetObligationByIdQuery(obl.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Confirmed");
        result.Value.Payment.Should().NotBeNull();
        result.Value.Payment!.Method.Should().Be("Transfer");
    }

    [Fact]
    public async Task GetObligationById_NonExistentId_ReturnsNotFound()
    {
        var handler = new GetObligationByIdQueryHandler(_repo);

        var result = await handler.Handle(
            new GetObligationByIdQuery("00000000000000000000000000"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}

internal sealed class FakeObligationRepository3 : IObligationRepository
{
    private readonly List<Obligation> _store = [];

    public void Seed(Obligation obl) => _store.Add(obl);

    public Task<Obligation?> GetByIdAsync(ObligationId id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(o => o.Id.Value == id.Value));

    public Task<IReadOnlyList<Obligation>> GetByStatusAsync(ObligationStatus? status, CancellationToken ct = default)
    {
        IReadOnlyList<Obligation> result = status is null
            ? _store.ToList()
            : _store.Where(o => o.Status == status).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Obligation obligation, CancellationToken ct = default)
    {
        _store.Add(obligation);
        return Task.CompletedTask;
    }

    public void Update(Obligation obligation) { }
}
