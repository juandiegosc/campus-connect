using Academic.Application.Students.SuspendStudent;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

/// <summary>
/// Unit tests for SuspendStudentCommandHandler (Phase 4 — ADR-067/068).
/// Reuses FakeStudentRepository2 + FakeIntegrationEventPublisher2 from
/// ConfirmStudentPaymentCommandHandlerTests.cs (same namespace — do NOT redeclare).
/// ESC-70..ESC-72 at handler level.
/// </summary>
public class SuspendStudentCommandHandlerTests
{
    private readonly FakeStudentRepository2          _repo      = new();
    private readonly FakeIntegrationEventPublisher2  _publisher = new();
    private readonly SuspendStudentCommandHandler    _handler;

    public SuspendStudentCommandHandlerTests()
        => _handler = new SuspendStudentCommandHandler(_repo, _publisher);

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

    /// <summary>ESC-73 — Student not found → 404, no persist/publish.</summary>
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFound_DoesNotPersistOrPublish()
    {
        var result = await _handler.Handle(
            new SuspendStudentCommand(StudentId.New(DateTimeOffset.UtcNow).Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-72 — Student already Graduated → 409 Conflict, no persist/publish.</summary>
    [Fact]
    public async Task Handle_GraduatedStudent_ReturnsConflict_DoesNotPersistOrPublish()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Graduated);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new SuspendStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("student.already_graduated");
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-70 — Active student → 200, AcademicStatus=Suspended, publishes StudentStatusUpdated.</summary>
    [Fact]
    public async Task Handle_ActiveStudent_TransitionsToSuspended_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Active);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new SuspendStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].AcademicStatus.Should().Be(AcademicStatus.Suspended);
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Suspended");
        published.FinancialStatus.Should().Be(student.FinancialStatus.ToString());
        published.StudentId.Should().Be(student.Id.Value);
    }

    /// <summary>ESC-71 — Already Suspended → idempotent 200, still publishes current status (REQ-AC4-12).</summary>
    [Fact]
    public async Task Handle_AlreadySuspendedStudent_IsIdempotent_StillPublishesSuspended()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Suspended);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new SuspendStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Suspended");
    }
}
