using Attendance.Application.Abstractions;
using Attendance.Application.Attendance.RecordAttendance;
using Attendance.Application.Students.Shared;
using Attendance.Domain.Attendance;
using Attendance.Domain.Incidents;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using MassTransit;
using Xunit;

namespace Attendance.Tests.Unit.Application;

public sealed class RecordAttendanceHandlerTests
{
    private static readonly string ValidStudentId = new('C', 26);
    private const string ValidDate   = "2026-07-15";
    private const string ValidStatus = "Present";

    private readonly FakeAttendanceRecordRepository  _repo     = new();
    private readonly FakeStudentReplicaRepository    _students = new();
    private readonly FakePublishEndpoint             _publish  = new();

    private RecordAttendanceCommandHandler CreateHandler()
        => new(_repo, _students, _publish);

    [Fact]
    public async Task Handle_UnknownStudentId_ReturnsFailureValidation()
    {
        // _students empty — student not found
        var cmd    = new RecordAttendanceCommand(ValidStudentId, ValidDate, ValidStatus);
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("student.not_found");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishCalledInsideHandler_BeforeUoWCommits()
    {
        _students.Seed(ValidStudentId);

        var cmd    = new RecordAttendanceCommand(ValidStudentId, ValidDate, ValidStatus);
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();

        // Publish must have been called during Handle (before UoW commits)
        _publish.PublishedCount.Should().Be(1, "AttendanceRecorded must be published once");
        _publish.LastPublished.Should().BeOfType<AttendanceRecorded>();
        _repo.Added.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        _students.Seed(ValidStudentId);

        var cmd    = new RecordAttendanceCommand(ValidStudentId, ValidDate, ValidStatus);
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Present");
        result.Value.RecordId.Should().HaveLength(26);
    }

    [Fact]
    public async Task Handle_MalformedDate_ReturnsValidationFailure()
    {
        _students.Seed(ValidStudentId);

        var cmd    = new RecordAttendanceCommand(ValidStudentId, "not-a-date", ValidStatus);
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ValidCommand_NoSaveChangesCalledByHandler()
    {
        _students.Seed(ValidStudentId);

        var cmd = new RecordAttendanceCommand(ValidStudentId, ValidDate, ValidStatus);
        await CreateHandler().Handle(cmd, default);

        _repo.SaveChangesCalled.Should().BeFalse("UoW owns commit; handler must NOT call SaveChanges");
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class FakeUlidGen : IUlidGenerator
{
    public string NewId(DateTimeOffset? timestamp = null)
        => NUlid.Ulid.NewUlid(timestamp ?? DateTimeOffset.UtcNow).ToString();
}

internal sealed class FakeAttendanceRecordRepository : IAttendanceRecordRepository
{
    public List<AttendanceRecord> Added { get; } = [];
    public bool SaveChangesCalled { get; private set; }

    public Task AddAsync(AttendanceRecord record, CancellationToken ct = default)
    {
        Added.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AttendanceRecordDto>> GetByStudentAsync(string studentId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AttendanceRecordDto>>(new List<AttendanceRecordDto>());
}

internal sealed class FakeIncidentRepository : IIncidentRepository
{
    public List<Incident> Added { get; } = [];

    public Task AddAsync(Incident incident, CancellationToken ct = default)
    {
        Added.Add(incident);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IncidentSummaryDto>> GetByStudentAsync(string studentId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<IncidentSummaryDto>>(new List<IncidentSummaryDto>());
}

internal sealed class FakeStudentReplicaRepository : IStudentReplicaRepository
{
    private readonly HashSet<string> _knownIds = [];

    public void Seed(string studentId) => _knownIds.Add(studentId);

    public Task UpsertAsync(string studentId, string fullName, string grade, string schoolId, DateTime lastUpdatedAt, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> ExistsAsync(string studentId, CancellationToken ct = default)
        => Task.FromResult(_knownIds.Contains(studentId));

    public Task<IReadOnlyList<StudentReplicaDto>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StudentReplicaDto>>(new List<StudentReplicaDto>());
}

internal sealed class FakePublishEndpoint : IPublishEndpoint
{
    public int PublishedCount { get; private set; }
    public object? LastPublished { get; private set; }

    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        PublishedCount++;
        LastPublished = message;
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedCount++;
        LastPublished = message;
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedCount++;
        LastPublished = message;
        return Task.CompletedTask;
    }

    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task Publish(object message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => throw new NotImplementedException();
}
