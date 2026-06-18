using Academic.Application.Abstractions;
using Academic.Application.Students.GetStudentById;
using Academic.Application.Students.GetStudentStatus;
using Academic.Application.Students.GetStudents;
using Academic.Application.Students.Shared;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

// ── Phase 4.5 + 4.6: Query handler tests ──

public class GetStudentByIdQueryHandlerTests
{
    private readonly GetStudentByIdQueryHandler      _handler;
    private readonly FakeStudentRepositoryForQueries _repo;

    public GetStudentByIdQueryHandlerTests()
    {
        _repo    = new FakeStudentRepositoryForQueries();
        _handler = new GetStudentByIdQueryHandler(_repo);
    }

    [Fact]
    public async Task GetStudentById_ExistingId_ReturnsAllFields()
    {
        var student = StudentBuilder.BuildStudent("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        _repo.SetStudent(student);

        var result = await _handler.Handle(new GetStudentByIdQuery("01ARZ3NDEKTSV4RRFFQ69G5FAV"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().Be(student.Id.Value);
        result.Value.FullName.Should().Be(student.FullName);
        result.Value.DocumentId.Should().Be(student.DocumentId.Value);
        result.Value.Grade.Should().Be(student.Grade);
        result.Value.Guardian.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStudentById_NonExistentId_ReturnsNotFound()
    {
        _repo.SetStudent(null);

        var result = await _handler.Handle(new GetStudentByIdQuery("01ARZ3NDEKTSV4RRFFQ69G5FAV"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}

public class GetStudentsQueryHandlerTests
{
    private readonly GetStudentsQueryHandler         _handler;
    private readonly FakeStudentRepositoryForQueries _repo;

    public GetStudentsQueryHandlerTests()
    {
        _repo    = new FakeStudentRepositoryForQueries();
        _handler = new GetStudentsQueryHandler(_repo);
    }

    [Fact]
    public async Task GetStudents_WithGradeFilter_ReturnsPaginatedResults()
    {
        var students = new List<Student>
        {
            StudentBuilder.BuildStudent("01ARZ3NDEKTSV4RRFFQ69G5FAV", "8vo EGB"),
            StudentBuilder.BuildStudent("01ARZ3NDEKTSV4RRFFQ69G5FBV", "8vo EGB")
        };
        _repo.SetPagedResult(students, 2);

        var result = await _handler.Handle(new GetStudentsQuery(1, 10, "8vo EGB", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
    }
}

public class GetStudentStatusQueryHandlerTests
{
    private readonly GetStudentStatusQueryHandler    _handler;
    private readonly FakeStudentRepositoryForQueries _repo;

    public GetStudentStatusQueryHandlerTests()
    {
        _repo    = new FakeStudentRepositoryForQueries();
        _handler = new GetStudentStatusQueryHandler(_repo);
    }

    [Fact]
    public async Task GetStudentStatus_ExistingId_ReturnsStatusDto()
    {
        var student = StudentBuilder.BuildStudent("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        _repo.SetStudent(student);

        var result = await _handler.Handle(new GetStudentStatusQuery("01ARZ3NDEKTSV4RRFFQ69G5FAV"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Exists.Should().BeTrue();
        result.Value.AcademicStatus.Should().Be("Active");
        result.Value.FinancialStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task GetStudentStatus_NonExistentId_ReturnsNotFound()
    {
        _repo.SetStudent(null);

        var result = await _handler.Handle(new GetStudentStatusQuery("01ARZ3NDEKTSV4RRFFQ69G5FAV"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}

// ── Shared fakes ──

internal sealed class FakeStudentRepositoryForQueries : IStudentRepository
{
    private Student?      _student;
    private List<Student> _pagedItems = [];
    private int           _pagedTotal;

    public void SetStudent(Student? s) => _student = s;
    public void SetPagedResult(List<Student> items, int total) { _pagedItems = items; _pagedTotal = total; }

    public Task<Student?> GetByIdAsync(StudentId id, CancellationToken ct = default)
        => Task.FromResult(_student);

    public Task<Student?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult(_student);

    public Task<bool> ExistsByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult(_student is not null);

    public Task AddAsync(Student student, CancellationToken ct = default) => Task.CompletedTask;

    public Task UpdateAsync(Student student, CancellationToken ct = default) => Task.CompletedTask;

    public Task<(IReadOnlyList<Student> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<Student>, int)>((_pagedItems, _pagedTotal));
}

file static class StudentBuilder
{
    public static Student BuildStudent(string id, string grade = "8vo EGB")
    {
        var docId        = DocumentId.TryCreate("0102030405").Result!;
        var guardian     = GuardianContact.TryCreate("María Gómez", "maria@example.com").Result!;
        var studentId    = StudentId.Parse(id);
        var enrollmentId = NUlid.Ulid.NewUlid().ToString();
        return Student.Create(studentId, "Luis Gómez", docId, grade, "SCH-001", guardian, enrollmentId, DateTime.UtcNow);
    }
}
