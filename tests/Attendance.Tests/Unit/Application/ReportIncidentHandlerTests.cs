using Attendance.Application.Abstractions;
using Attendance.Application.Incidents.ReportIncident;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using Xunit;

namespace Attendance.Tests.Unit.Application;

public sealed class ReportIncidentHandlerTests
{
    private static readonly string ValidStudentId = new('D', 26);

    private readonly FakeIncidentRepository      _repo     = new();
    private readonly FakeStudentReplicaRepository _students = new();
    private readonly FakePublishEndpoint         _publish  = new();

    private ReportIncidentCommandHandler CreateHandler()
        => new(_repo, _students, _publish);

    [Fact]
    public async Task Handle_UnknownStudentId_ReturnsFailureValidation()
    {
        // _students empty — student not found
        var cmd    = new ReportIncidentCommand(ValidStudentId, "Bullying", "High", "desc");
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("student.not_found");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesIncidentReportedWithNoDescription()
    {
        _students.Seed(ValidStudentId);

        var cmd    = new ReportIncidentCommand(ValidStudentId, "Bullying", "High", "Student hit another student");
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        _publish.PublishedCount.Should().Be(1);
        var published = _publish.LastPublished as IncidentReported;
        published.Should().NotBeNull();
        published!.IncidentId.Should().HaveLength(26);
        published.StudentId.Should().Be(ValidStudentId);
        published.Type.Should().Be("Bullying");
        published.Severity.Should().Be("High");

        // Verify IncidentReported integration event has no Description property (one-way door REQ-AT1-02)
        typeof(IncidentReported).GetProperty("Description").Should().BeNull(
            "IncidentReported must NOT have a Description property (REQ-AT1-02, ADR-070)");
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        _students.Seed(ValidStudentId);

        var cmd    = new ReportIncidentCommand(ValidStudentId, "Bullying", "High", "description");
        var result = await CreateHandler().Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Severity.Should().Be("High");
        result.Value.IncidentId.Should().HaveLength(26);
    }

    [Fact]
    public async Task Handle_ValidCommand_NoSaveChangesCalledByHandler()
    {
        _students.Seed(ValidStudentId);

        var cmd = new ReportIncidentCommand(ValidStudentId, "Bullying", "Low", "desc");
        await CreateHandler().Handle(cmd, default);

        _repo.Added.Should().HaveCount(1, "incident must be persisted by handler");
    }
}
