using Academic.Application.Abstractions;
using Academic.Application.Students.EnrollStudent;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Students;

public class EnrollStudentCommandHandlerTests
{
    private readonly FakeStudentRepository            _repo;
    private readonly FakeUlidGenerator                _ulid;
    private readonly FakeIntegrationEventPublisher    _publisher;
    private readonly EnrollStudentCommandHandler      _handler;

    public EnrollStudentCommandHandlerTests()
    {
        _repo      = new FakeStudentRepository();
        _ulid      = new FakeUlidGenerator();
        _publisher = new FakeIntegrationEventPublisher();
        _handler   = new EnrollStudentCommandHandler(_repo, _ulid, _publisher);
    }

    private static EnrollStudentCommand ValidCommand(string documentId = "0102030405")
        => new("Luis Gómez", documentId, "8vo EGB", "SCH-001", "María Gómez", "maria@example.com");

    [Fact]
    public async Task Handle_HappyPath_ReturnsSuccessWithUlidIds()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentId.Should().NotBeNullOrEmpty();
        result.Value.EnrollmentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_HappyPath_StudentIdIs26Chars()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.Value.StudentId.Length.Should().Be(26);
    }

    [Fact]
    public async Task Handle_HappyPath_StatusIsActive()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_DuplicateDocumentId_ReturnsConflict()
    {
        _repo.SetExistsResult(true);

        var result = await _handler.Handle(ValidCommand("0102030405"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_DuplicateDocumentId_DoesNotPersist()
    {
        _repo.SetExistsResult(true);

        await _handler.Handle(ValidCommand("0102030405"), CancellationToken.None);

        _repo.AddedStudents.Should().BeEmpty();
    }
}

public class EnrollStudentCommandValidatorTests
{
    private readonly EnrollStudentCommandValidator _validator = new();

    [Fact]
    public void Validator_InvalidDocumentIdFormat_FailsValidation()
    {
        var cmd = new EnrollStudentCommand("Luis Gómez", "AB", "8vo EGB", "SCH-001", "María Gómez", "maria@example.com");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentId");
    }

    [Fact]
    public void Validator_InvalidGuardianEmail_FailsValidation()
    {
        var cmd = new EnrollStudentCommand("Luis Gómez", "0102030405", "8vo EGB", "SCH-001", "María Gómez", "not-an-email");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GuardianEmail");
    }
}

// ── Fakes ──

internal sealed class FakeStudentRepository : IStudentRepository
{
    private bool _existsResult;
    private Student? _storedStudent;
    public List<Student> AddedStudents   { get; } = [];
    /// <summary>Captures students passed to UpdateAsync so handler tests can assert on mutations (REQ-AC2-09).</summary>
    public List<Student> UpdatedStudents { get; } = [];

    public void SetExistsResult(bool value)    => _existsResult   = value;
    public void SetStoredStudent(Student? s)   => _storedStudent  = s;

    public Task<Student?> GetByIdAsync(StudentId id, CancellationToken ct = default)
        => Task.FromResult(_storedStudent?.Id == id ? _storedStudent : null);

    public Task<Student?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult<Student?>(null);

    public Task<bool> ExistsByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => Task.FromResult(_existsResult);

    public Task AddAsync(Student student, CancellationToken ct = default)
    {
        AddedStudents.Add(student);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Captures the student for assertion (ESC-54 in unit tests — no EF Core needed).
    /// Required by REQ-AC2-09 so unit tests can verify UpdateAsync was called with the mutated student.
    /// </summary>
    public Task UpdateAsync(Student student, CancellationToken ct = default)
    {
        UpdatedStudents.Add(student);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Student> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<Student>, int)>(([], 0));
}

internal sealed class FakeUlidGenerator : IUlidGenerator
{
    private readonly Queue<string> _ids = new();

    public void Enqueue(params string[] ids)
    {
        foreach (var id in ids) _ids.Enqueue(id);
    }

    public string NewId(DateTimeOffset? timestamp = null)
        => _ids.Count > 0 ? _ids.Dequeue() : NUlid.Ulid.NewUlid().ToString();
}

internal sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    /// <summary>Captures all published integration events for assertion (ESC-48..ESC-53).</summary>
    public List<object> Published { get; } = [];

    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
