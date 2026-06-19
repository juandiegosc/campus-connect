using Academic.Application.Students.GraduateStudent;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

/// <summary>
/// Unit tests for GraduateStudentCommandHandler (Phase 4 — ADR-066/067/068).
/// Reuses FakeStudentRepository2 + FakeIntegrationEventPublisher2 from
/// ConfirmStudentPaymentCommandHandlerTests.cs (same namespace — do NOT redeclare).
/// Graduate is TERMINAL: already-Graduated is 409, NOT idempotent no-op (ADR-066).
/// ESC-83..ESC-86 at handler level.
/// </summary>
public class GraduateStudentCommandHandlerTests
{
    private readonly FakeStudentRepository2         _repo      = new();
    private readonly FakeIntegrationEventPublisher2 _publisher = new();
    private readonly GraduateStudentCommandHandler  _handler;

    public GraduateStudentCommandHandlerTests()
        => _handler = new GraduateStudentCommandHandler(_repo, _publisher);

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

    /// <summary>ESC-86 — Student not found → 404, no persist/publish.</summary>
    [Fact]
    public async Task Handle_StudentNotFound_ReturnsNotFound_DoesNotPersistOrPublish()
    {
        var result = await _handler.Handle(
            new GraduateStudentCommand(StudentId.New(DateTimeOffset.UtcNow).Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-85 — Student already Graduated → 409 Conflict (TERMINAL — NOT idempotent, ADR-066).</summary>
    [Fact]
    public async Task Handle_AlreadyGraduatedStudent_ReturnsConflict_DoesNotPersistOrPublish()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Graduated);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new GraduateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("student.already_graduated");
        _repo.UpdatedStudents.Should().BeEmpty();
        _publisher.Published.Should().BeEmpty();
    }

    /// <summary>ESC-83 — Active student → 200, AcademicStatus=Graduated, publishes StudentStatusUpdated.</summary>
    [Fact]
    public async Task Handle_ActiveStudent_TransitionsToGraduated_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Active);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new GraduateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].AcademicStatus.Should().Be(AcademicStatus.Graduated);
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Graduated");
        published.FinancialStatus.Should().Be(student.FinancialStatus.ToString());
        published.StudentId.Should().Be(student.Id.Value);
    }

    /// <summary>ESC-84 — Suspended student → 200, AcademicStatus=Graduated, publishes StudentStatusUpdated.</summary>
    [Fact]
    public async Task Handle_SuspendedStudent_TransitionsToGraduated_PublishesStudentStatusUpdated()
    {
        var student = BuildStudentWithAcademicStatus(AcademicStatus.Suspended);
        _repo.SetStoredStudent(student);

        var result = await _handler.Handle(new GraduateStudentCommand(student.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.UpdatedStudents.Should().ContainSingle();
        _repo.UpdatedStudents[0].AcademicStatus.Should().Be(AcademicStatus.Graduated);
        var published = _publisher.Published[0].Should().BeOfType<StudentStatusUpdated>().Subject;
        published.AcademicStatus.Should().Be("Graduated");
    }
}
