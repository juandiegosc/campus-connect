using Academic.Application.Abstractions;
using Academic.Application.Students.ConfirmPayment;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

/// <summary>
/// Unit tests for ConfirmStudentPaymentCommandHandler.
/// Uses in-memory fakes — no MassTransit, no DB (REQ-AC2-09).
/// ESC-48..ESC-53.
/// </summary>
public class ConfirmStudentPaymentCommandHandlerTests
{
    private readonly FakeStudentRepository2        _repo;
    private readonly FakeIntegrationEventPublisher2 _publisher;
    private readonly ConfirmStudentPaymentCommandHandler _handler;

    public ConfirmStudentPaymentCommandHandlerTests()
    {
        _repo      = new FakeStudentRepository2();
        _publisher = new FakeIntegrationEventPublisher2();
        _handler   = new ConfirmStudentPaymentCommandHandler(_repo, _publisher);
    }

    private static Student BuildStudentWithStatus(FinancialStatus status)
    {
        var docId     = DocumentId.TryCreate("0102030405").Result!;
        var guardian  = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId = StudentId.New(DateTimeOffset.UtcNow);
        var enrollId  = StudentId.New(DateTimeOffset.UtcNow.AddMilliseconds(1)).Value;
        var student   = Student.Create(studentId, "Luis Gómez", docId, "8vo EGB", "SCH-001", guardian, enrollId, DateTime.UtcNow);

        if (status != FinancialStatus.Pending)
        {
            // Force the requested status via reflection (Overdue/Paid only set externally)
            typeof(Student)
                .GetProperty(nameof(Student.FinancialStatus))!
                .SetValue(student, status);
        }

        return student;
    }

    /// <summary>ESC-48 — Happy path: Pending → Paid</summary>
    [Fact]
    public async Task Handle_PendingStudent_TransitionsToPaid_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Pending);
        _repo.SetStoredStudent(student);
        var cmd = new ConfirmStudentPaymentCommand(student.Id.Value, "PAY-001", "corr-001");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle(s => s.Id == student.Id);
        _repo.UpdatedStudents[0].FinancialStatus.Should().Be(FinancialStatus.Paid);
        _publisher.Published.Should().ContainSingle();
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.FinancialStatus.Should().Be("Paid");
        published.AcademicStatus.Should().Be("Active");
        published.CorrelationId.Should().Be("corr-001");
        published.StudentId.Should().Be(student.Id.Value);
    }

    /// <summary>ESC-49 — Overdue → Paid</summary>
    [Fact]
    public async Task Handle_OverdueStudent_TransitionsToPaid_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Overdue);
        _repo.SetStoredStudent(student);
        var cmd = new ConfirmStudentPaymentCommand(student.Id.Value, "PAY-002", "corr-002");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].FinancialStatus.Should().Be(FinancialStatus.Paid);
        _publisher.Published.Should().ContainSingle();
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.FinancialStatus.Should().Be("Paid");
    }

    /// <summary>ESC-50 — Paid → no-op (idempotent), still publishes current status</summary>
    [Fact]
    public async Task Handle_AlreadyPaidStudent_IsNoOp_StillPublishesCurrentStatus()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Paid);
        _repo.SetStoredStudent(student);
        var cmd = new ConfirmStudentPaymentCommand(student.Id.Value, "PAY-003", "corr-003");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();      // persist always called
        _publisher.Published.Should().ContainSingle();       // publish always called
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.FinancialStatus.Should().Be("Paid");
    }

    /// <summary>ESC-51 — StudentId not found → Result.Failure(NotFound), no persist/publish</summary>
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFoundFailure_DoesNotPersistOrPublish()
    {
        // Repo returns null for any ID
        var cmd = new ConfirmStudentPaymentCommand(StudentId.New(DateTimeOffset.UtcNow).Value, "PAY-004", "corr-004");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-52 — CorrelationId propagated to StudentStatusUpdated</summary>
    [Fact]
    public async Task Handle_CorrelationIdPropagated_ToStudentStatusUpdated()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Pending);
        _repo.SetStoredStudent(student);
        var cmd = new ConfirmStudentPaymentCommand(student.Id.Value, "PAY-005", "corr-test-007");

        await _handler.Handle(cmd, CancellationToken.None);

        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.CorrelationId.Should().Be("corr-test-007");
    }

    /// <summary>ESC-53 — IIntegrationEventPublisher called exactly once per successful invocation</summary>
    [Theory]
    [InlineData(FinancialStatus.Pending)]
    [InlineData(FinancialStatus.Paid)]
    [InlineData(FinancialStatus.Overdue)]
    public async Task Handle_PublisherCalledExactlyOnce_ForEachSuccessfulInvocation(FinancialStatus status)
    {
        var student = BuildStudentWithStatus(status);
        _repo.SetStoredStudent(student);
        var cmd = new ConfirmStudentPaymentCommand(student.Id.Value, "PAY-006", "corr-006");

        await _handler.Handle(cmd, CancellationToken.None);

        _publisher.Published.Should().HaveCount(1);
    }
}

// ── Fakes for ConfirmStudentPaymentCommandHandler tests ──
// Named with suffix 2 to avoid collision with fakes in EnrollStudentCommandHandlerTests.cs
// in the same namespace. Consider extracting to shared Fakes file in a later phase.

internal sealed class FakeStudentRepository2 : IStudentRepository
{
    private Student? _storedStudent;
    public List<Student> UpdatedStudents { get; } = [];

    public void SetStoredStudent(Student? s) => _storedStudent = s;

    public Task<Student?> GetByIdAsync(StudentId id, CancellationToken ct = default)
        => Task.FromResult(_storedStudent?.Id == id ? _storedStudent : null);

    public Task<Student?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult<Student?>(null);

    public Task<bool> ExistsByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task AddAsync(Student student, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UpdateAsync(Student student, CancellationToken ct = default)
    {
        UpdatedStudents.Add(student);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Student> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<Student>, int)>(([], 0));
}

internal sealed class FakeIntegrationEventPublisher2 : IIntegrationEventPublisher
{
    public List<object> Published { get; } = [];

    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
