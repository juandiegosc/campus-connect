using Academic.Application.Students.MarkOverdue;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

/// <summary>
/// Unit tests for MarkStudentOverdueCommandHandler (Phase 3, ADR-063).
/// Reuses the in-memory fakes (FakeStudentRepository2 / FakeIntegrationEventPublisher2) declared
/// in ConfirmStudentPaymentCommandHandlerTests.cs (same namespace). No DB, no MassTransit.
/// ESC-66..ESC-69.
/// </summary>
public class MarkStudentOverdueCommandHandlerTests
{
    private readonly FakeStudentRepository2         _repo      = new();
    private readonly FakeIntegrationEventPublisher2 _publisher = new();
    private readonly MarkStudentOverdueCommandHandler _handler;

    public MarkStudentOverdueCommandHandlerTests()
        => _handler = new MarkStudentOverdueCommandHandler(_repo, _publisher);

    private static Student BuildStudentWithStatus(FinancialStatus status)
    {
        var docId     = DocumentId.TryCreate("0102030405").Result!;
        var guardian  = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId = StudentId.New(DateTimeOffset.UtcNow);
        var enrollId  = StudentId.New(DateTimeOffset.UtcNow.AddMilliseconds(1)).Value;
        var student   = Student.Create(studentId, "Luis Gómez", docId, "8vo EGB", "SCH-001", guardian, enrollId, DateTime.UtcNow);

        if (status != FinancialStatus.Pending)
            typeof(Student).GetProperty(nameof(Student.FinancialStatus))!.SetValue(student, status);

        return student;
    }

    /// <summary>ESC-66 — Pending → Overdue, persists and publishes StudentStatusUpdated.</summary>
    [Fact]
    public async Task Handle_PendingStudent_TransitionsToOverdue_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Pending);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new MarkStudentOverdueCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].FinancialStatus.Should().Be(FinancialStatus.Overdue);
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.FinancialStatus.Should().Be("Overdue");
        published.AcademicStatus.Should().Be("Active");
        published.StudentId.Should().Be(student.Id.Value);
    }

    /// <summary>ESC-67 — Already Paid → 409 Conflict, no persist/publish.</summary>
    [Fact]
    public async Task Handle_PaidStudent_ReturnsConflict_DoesNotPersistOrPublish()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Paid);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new MarkStudentOverdueCommand(student.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-68 — Not found → 404, no persist/publish.</summary>
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFound_DoesNotPersistOrPublish()
    {
        var result = await _handler.Handle(
            new MarkStudentOverdueCommand(StudentId.New(DateTimeOffset.UtcNow).Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-69 — Already Overdue → idempotent success, still publishes current status.</summary>
    [Fact]
    public async Task Handle_AlreadyOverdueStudent_IsIdempotent_StillPublishesOverdue()
    {
        var student = BuildStudentWithStatus(FinancialStatus.Overdue);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new MarkStudentOverdueCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.FinancialStatus.Should().Be("Overdue");
    }
}
