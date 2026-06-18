using BuildingBlocks.Application.Common;
using FluentAssertions;
using FluentValidation;
using MassTransit;
using Payments.Application.Abstractions;
using Payments.Application.Obligations.RegisterObligation;
using Payments.Application.Students.Shared;
using Payments.Domain.Obligations;
using Payments.Tests.Students;
using Xunit;

namespace Payments.Tests.Obligations;

/// <summary>
/// Unit tests for RegisterObligationCommandHandler and its validator.
/// ESC-PM-01..ESC-PM-04, REQ-PM1-01, REQ-PM1-02.
/// Phase 2: ESC-PM-37, ESC-PM-38, REQ-PM2-04, REQ-PM2-05 (ADR-056).
/// </summary>
public sealed class RegisterObligationCommandHandlerTests
{
    private readonly FakeObligationRepository     _repo     = new();
    private readonly FakeUlidGenerator            _ulid     = new();
    private readonly FakeStudentReplicaRepository _students = new();

    private RegisterObligationCommandHandler CreateHandler()
        => new(_repo, _students, _ulid);

    private static RegisterObligationCommand ValidCommand(string? studentId = null) =>
        new(
            studentId ?? "01234567890123456789012345",
            "Mensualidad Enero",
            100m,
            DateTime.UtcNow.AddDays(30));

    // Phase 2 note: happy-path tests must seed a student replica first (existence guard ADR-056).
    private const string KnownStudentId = "01234567890123456789012345";

    private void SeedKnownStudent()
        => _students.Seed(KnownStudentId, "Ana Torres", "5A", "SCH-001");

    [Fact]
    public async Task Handle_HappyPath_ReturnsSuccess_WithObligationIdAnd26Chars()
    {
        SeedKnownStudent();
        var result = await CreateHandler().Handle(ValidCommand(KnownStudentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ObligationId.Length.Should().Be(26);
    }

    [Fact]
    public async Task Handle_HappyPath_StatusIsPending()
    {
        SeedKnownStudent();
        var result = await CreateHandler().Handle(ValidCommand(KnownStudentId), CancellationToken.None);

        result.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_HappyPath_CallsAddAsync_OnRepository()
    {
        SeedKnownStudent();
        await CreateHandler().Handle(ValidCommand(KnownStudentId), CancellationToken.None);

        _repo.Added.Should().HaveCount(1);
        _repo.SaveChangesCalled.Should().BeFalse("UoW owns commit, handler must not call SaveChanges");
    }

    [Fact]
    public void Validator_AmountZero_FailsValidation()
    {
        var validator = new RegisterObligationCommandValidator();
        var result = validator.Validate(new RegisterObligationCommand(
            "01234567890123456789012345", "Concept", 0m, DateTime.UtcNow.AddDays(1)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AmountNegative_FailsValidation()
    {
        var validator = new RegisterObligationCommandValidator();
        var result = validator.Validate(new RegisterObligationCommand(
            "01234567890123456789012345", "Concept", -5m, DateTime.UtcNow.AddDays(1)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ConceptEmpty_FailsValidation()
    {
        var validator = new RegisterObligationCommandValidator();
        var result = validator.Validate(new RegisterObligationCommand(
            "01234567890123456789012345", "", 100m, DateTime.UtcNow.AddDays(1)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_StudentIdInvalidFormat_FailsValidation()
    {
        var validator = new RegisterObligationCommandValidator();
        var result = validator.Validate(new RegisterObligationCommand(
            "short", "Concept", 100m, DateTime.UtcNow.AddDays(1)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_DueDateDefault_FailsValidation()
    {
        var validator = new RegisterObligationCommandValidator();
        var result = validator.Validate(new RegisterObligationCommand(
            "01234567890123456789012345", "Concept", 100m, default));
        result.IsValid.Should().BeFalse();
    }

    // ── Phase 2: existence guard (ADR-056, REQ-PM2-04) ────────────────────────

    /// <summary>
    /// 3.1 RED: StudentId not in replica store → Failure with Error.Validation (NOT Error.NotFound).
    /// ADR-056: Payments MapError maps Validation→400, NotFound→404. Guard MUST use Error.Validation.
    /// </summary>
    [Fact]
    public async Task Handle_UnknownStudentId_ReturnsFailure_ValidationError()
    {
        // FakeStudentReplicaRepository starts empty — "STU-UNKNOWN" is not seeded.
        var result = await CreateHandler().Handle(
            ValidCommand("01234567890123456789012345"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("unknown student must be rejected");
        result.Error.Code.Should().Be("student.not_found");
        result.Error.Type.Should().Be(ErrorType.Validation,
            "ADR-056: must be Validation so MapError returns HTTP 400 (not 404)");
    }

    /// <summary>
    /// 3.1 RED: Known StudentId (replica row exists) → Success.
    /// </summary>
    [Fact]
    public async Task Handle_KnownStudentId_ReturnsSuccess()
    {
        // Pre-seed the replica so ExistsAsync returns true.
        const string studentId = "01234567890123456789012345";
        _students.Seed(studentId, "Ana Torres", "5A", "SCH-001");

        var result = await CreateHandler().Handle(
            ValidCommand(studentId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("known student must pass the guard");
        result.Value.Status.Should().Be("Pending");
        _repo.Added.Should().HaveCount(1, "obligation must be persisted");
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class FakeUlidGenerator : IUlidGenerator
{
    public string NewId(DateTimeOffset? timestamp = null)
        => NUlid.Ulid.NewUlid(timestamp ?? DateTimeOffset.UtcNow).ToString();
}

internal sealed class FakeObligationRepository : IObligationRepository
{
    public List<Obligation> Added { get; } = [];
    public List<Obligation> Updated { get; } = [];
    public bool SaveChangesCalled { get; private set; }
    private readonly List<Obligation> _store = [];

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
        Added.Add(obligation);
        return Task.CompletedTask;
    }

    public void Update(Obligation obligation)
    {
        Updated.Add(obligation);
        // simulate EF dirty tracking — no SaveChanges here
    }

    public void SimulateSaveChanges() => SaveChangesCalled = true;

    public void Seed(Obligation obl) => _store.Add(obl);
}
