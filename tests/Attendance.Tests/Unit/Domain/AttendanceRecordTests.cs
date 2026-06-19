using Attendance.Domain.Attendance;
using Attendance.Domain.Attendance.Events;
using FluentAssertions;
using Xunit;

namespace Attendance.Tests.Unit.Domain;

public sealed class AttendanceRecordTests
{
    private static AttendanceRecordId NewId() => AttendanceRecordId.New(DateTimeOffset.UtcNow);
    private static readonly string ValidStudentId = new('A', 26);

    [Fact]
    public void Record_WithValidInputs_ReturnsSuccess()
    {
        var id     = NewId();
        var date   = new DateOnly(2026, 7, 15);
        var result = AttendanceRecord.Record(ValidStudentId, date, AttendanceStatus.Present, DateTime.UtcNow, id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.StudentId.Should().Be(ValidStudentId);
        result.Value.Date.Should().Be(date);
        result.Value.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public void Record_WithValidInputs_RaisesOneAttendanceRecordedDomainEvent()
    {
        var id     = NewId();
        var date   = new DateOnly(2026, 7, 15);
        var result = AttendanceRecord.Record(ValidStudentId, date, AttendanceStatus.Present, DateTime.UtcNow, id);

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().HaveCount(1);
        result.Value.DomainEvents.Single().Should().BeOfType<AttendanceRecordedDomainEvent>();
    }

    [Fact]
    public void Record_WithValidInputs_DomainEventHasCorrectDateIso()
    {
        var id     = NewId();
        var date   = new DateOnly(2026, 7, 15);
        var result = AttendanceRecord.Record(ValidStudentId, date, AttendanceStatus.Present, DateTime.UtcNow, id);

        var evt = (AttendanceRecordedDomainEvent)result.Value.DomainEvents.Single();
        evt.Date.Should().Be("2026-07-15");
    }

    [Fact]
    public void Record_WithValidInputs_DomainEventHasStatusAsString()
    {
        var id     = NewId();
        var date   = new DateOnly(2026, 7, 15);
        var result = AttendanceRecord.Record(ValidStudentId, date, AttendanceStatus.Absent, DateTime.UtcNow, id);

        var evt = (AttendanceRecordedDomainEvent)result.Value.DomainEvents.Single();
        evt.Status.Should().Be("Absent");
    }

    [Fact]
    public void Record_WithEmptyStudentId_ReturnsFailure()
    {
        var result = AttendanceRecord.Record(string.Empty, new DateOnly(2026, 7, 15), AttendanceStatus.Present, DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("student_id");
    }

    [Fact]
    public void Record_WithStudentIdNotTwentySixChars_ReturnsFailure()
    {
        var result = AttendanceRecord.Record("short", new DateOnly(2026, 7, 15), AttendanceStatus.Present, DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("student_id");
    }

    [Fact]
    public void Record_WithMinValueDate_ReturnsFailure()
    {
        var result = AttendanceRecord.Record(ValidStudentId, DateOnly.MinValue, AttendanceStatus.Present, DateTime.UtcNow, NewId());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("date");
    }

    [Fact]
    public void AttendanceStatus_TryCreate_WithInvalidValue_ReturnsFailure()
    {
        var result = AttendanceStatusExtensions.TryCreate("OnVacation");

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(BuildingBlocks.Application.Common.ErrorType.Validation);
    }

    [Fact]
    public void AttendanceStatus_TryCreate_WithValidValue_ReturnsSuccess()
    {
        var result = AttendanceStatusExtensions.TryCreate("Present");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(AttendanceStatus.Present);
    }
}
