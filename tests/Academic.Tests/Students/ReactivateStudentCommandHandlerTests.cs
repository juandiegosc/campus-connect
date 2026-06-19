using Academic.Application.Students.ReactivateStudent;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

/// <summary>
/// Unit tests for ReactivateStudentCommandHandler (Phase 4 — ADR-067/068).
/// Reuses FakeStudentRepository2 + FakeIntegrationEventPublisher2 from
/// ConfirmStudentPaymentCommandHandlerTests.cs (same namespace — do NOT redeclare).
/// ESC-77..ESC-80 at handler level.
/// </summary>
public class ReactivateStudentCommandHandlerTests
{
    private readonly FakeStudentRepository2          _repo      = new();
    private readonly FakeIntegrationEventPublisher2  _publisher = new();
    private readonly ReactivateStudentCommandHandler _handler;

    public ReactivateStudentCommandHandlerTests()
        => _handler = new ReactivateStudentCommandHandler(_repo, _publisher);

    private static Student BuildStudentWithAcademicStatus(AcademicStatus status)
    {
        var docId     = DocumentId.TryCreate("0102030405").Result!;
        var guardian  = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId = StudentId.New(DateTimeOffset.UtcNow);
        var enrollId  = StudentId.New(DateTimeOffset.UtcNow.AddMilliseconds(1)).Value;
        var student   = Student.Create(studentId, "Luis Gómez", docId, "8vo EGB", "SCH-001", guardian, enrollId, DateTime.UtcNow);

        if (status != AcademicStatus.Active)
            typeof(Student).GetProperty(nameof(Student.AcademicStatus))!.SetValue(student, status);

        return student;
    }

    /// <summary>ESC-80 — Student not found → 404, no persist/publish.</summary>
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFound_DoesNotPersistOrPublish()
    {
        var result = await _handler.Handle(
            new ReactivateStudentCommand(StudentId.New(DateTimeOffset.UtcNow).Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-79 — Student already Graduated → 409 Conflict, no persist/publish.</summary>
    [Fact]
    public async Task Handle_GraduatedStudent_ReturnsConflict_DoesNotPersistOrPublish()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Graduated);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new ReactivateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("student.already_graduated");
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-77 — Suspended student → 200, AcademicStatus=Active, publishes StudentStatusUpdated.</summary>
    [Fact]
    public async Task Handle_SuspendedStudent_TransitionsToActive_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Suspended);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new ReactivateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].AcademicStatus.Should().Be(AcademicStatus.Active);
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Active");
        published.FinancialStatus.Should().Be(student.FinancialStatus.ToString());
        published.StudentId.Should().Be(student.Id.Value);
    }

    /// <summary>ESC-78 — Already Active → idempotent 200, still publishes current status (REQ-AC4-12).</summary>
    [Fact]
    public async Task Handle_AlreadyActiveStudent_IsIdempotent_StillPublishesActive()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Active);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new ReactivateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Active");
    }
}
