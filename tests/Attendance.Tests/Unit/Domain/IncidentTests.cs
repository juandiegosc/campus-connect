using Attendance.Domain.Incidents;
using Attendance.Domain.Incidents.Events;
using FluentAssertions;
using Xunit;

namespace Attendance.Tests.Unit.Domain;

public sealed class IncidentTests
{
    private static IncidentId NewId() => IncidentId.New(DateTimeOffset.UtcNow);
    private static readonly string ValidStudentId = new('B', 26);

    [Fact]
    public void Report_WithValidInputs_ReturnsSuccess()
    {
        var result = Incident.Report(ValidStudentId, "Bullying", IncidentSeverity.High,
            "Student hit another student", DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.StudentId.Should().Be(ValidStudentId);
        result.Value.Type.Should().Be("Bullying");
        result.Value.Severity.Should().Be(IncidentSeverity.High);
        result.Value.Description.Should().Be("Student hit another student");
    }

    [Fact]
    public void Report_WithValidInputs_RaisesOneIncidentReportedDomainEvent()
    {
        var result = Incident.Report(ValidStudentId, "Bullying", IncidentSeverity.High,
            "Student hit another student", DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().HaveCount(1);
        result.Value.DomainEvents.Single().Should().BeOfType<IncidentReportedDomainEvent>();
    }

    [Fact]
    public void Report_DomainEventDoesNotContainDescription()
    {
        // ESC-AT-24: domain event MUST NOT carry description (one-way door)
        var result = Incident.Report(ValidStudentId, "Bullying", IncidentSeverity.High,
            "some description", DateTime.UtcNow, NewId());

        var evt = (IncidentReportedDomainEvent)result.Value.DomainEvents.Single();

        // Verify the domain event record type has no Description property
        var type = typeof(IncidentReportedDomainEvent);
        type.GetProperty("Description").Should().BeNull(
            "IncidentReportedDomainEvent must NOT have a Description property (REQ-AT1-13)");

        evt.IncidentId.Should().NotBeEmpty();
        evt.StudentId.Should().Be(ValidStudentId);
        evt.Type.Should().Be("Bullying");
        evt.Severity.Should().Be("High");
    }

    [Fact]
    public void Report_WithEmptyType_ReturnsFailure()
    {
        var result = Incident.Report(ValidStudentId, string.Empty, IncidentSeverity.Low,
            "desc", DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("type");
    }

    [Fact]
    public void Report_WithEmptyDescription_ReturnsFailure()
    {
        var result = Incident.Report(ValidStudentId, "Bullying", IncidentSeverity.Low,
            string.Empty, DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("description");
    }

    [Fact]
    public void IncidentSeverity_TryCreate_WithInvalidValue_ReturnsFailure()
    {
        var result = IncidentSeverityExtensions.TryCreate("Critical");

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(BuildingBlocks.Application.Common.ErrorType.Validation);
    }

    [Fact]
    public void IncidentSeverity_TryCreate_WithValidValue_ReturnsSuccess()
    {
        var result = IncidentSeverityExtensions.TryCreate("High");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(IncidentSeverity.High);
    }
}
