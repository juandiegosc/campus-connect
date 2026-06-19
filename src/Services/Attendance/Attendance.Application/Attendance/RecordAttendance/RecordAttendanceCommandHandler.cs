using Attendance.Application.Abstractions;
using Attendance.Domain.Attendance;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MassTransit;
using MediatR;

namespace Attendance.Application.Attendance.RecordAttendance;

/// <summary>
/// Handler for RecordAttendanceCommand.
/// CRITICAL flow order (ADR-075, REQ-AT1-19):
///   1. StudentId existence guard → 400 if not found (REQ-AT1-21, ADR-056)
///   2. Parse Date string → DateOnly (400 on malformed — ADR-074)
///   3. Parse Status enum → 400 on invalid
///   4. Generate AttendanceRecordId
///   5. Create AttendanceRecord aggregate (validates invariants)
///   6. repo.AddAsync — EF tracks; NO SaveChanges
///   7. ★ BEFORE returning: IPublishEndpoint.Publish(AttendanceRecorded) — OutboxMessage INSERT
///   8. Return Result.Success
///   UnitOfWorkBehavior commits attendance_records INSERT + OutboxMessage INSERT atomically.
/// </summary>
public sealed class RecordAttendanceCommandHandler(
    IAttendanceRecordRepository  repo,
    IStudentReplicaRepository    students,
    IPublishEndpoint             publishEndpoint)
    : IRequestHandler<RecordAttendanceCommand, Result<RecordAttendanceResponse>>
{
    public async Task<Result<RecordAttendanceResponse>> Handle(
        RecordAttendanceCommand command,
        CancellationToken       cancellationToken)
    {
        // 1. Existence guard — BEFORE creating the record (REQ-AT1-21, ADR-056)
        // Error.Validation so MapError yields HTTP 400 (NOT 404 — ADR-056 carry-forward from Payments)
        if (!await students.ExistsAsync(command.StudentId, cancellationToken))
            return Result<RecordAttendanceResponse>.Failure(
                Error.Validation("student.not_found",
                    $"Student {command.StudentId} is not known to Attendance service."));

        // 2. Parse date string → DateOnly (ADR-074)
        if (!DateOnly.TryParseExact(command.Date, "yyyy-MM-dd", out var date))
            return Result<RecordAttendanceResponse>.Failure(
                Error.Validation("date.invalid",
                    $"Date '{command.Date}' is not valid. Expected format: yyyy-MM-dd."));

        // 3. Parse status enum
        var statusResult = AttendanceStatusExtensions.TryCreate(command.Status);
        if (!statusResult.IsSuccess)
            return Result<RecordAttendanceResponse>.Failure(statusResult.Error);

        // 4. Generate ID
        var now = DateTimeOffset.UtcNow;
        var id  = AttendanceRecordId.New(now);

        // 5. Create aggregate (validates invariants)
        var recordResult = AttendanceRecord.Record(command.StudentId, date, statusResult.Value, now.UtcDateTime, id);
        if (!recordResult.IsSuccess)
            return Result<RecordAttendanceResponse>.Failure(recordResult.Error);

        // 6. Persist (EF tracks; UoW commits after handler returns)
        await repo.AddAsync(recordResult.Value, cancellationToken);

        // 7. ★ CRITICAL (ADR-075, REQ-AT1-19): Publish BEFORE returning
        // UnitOfWorkBehavior saves atomically AFTER this handler returns.
        await publishEndpoint.Publish(new AttendanceRecorded
        {
            RecordId  = id.Value,
            StudentId = command.StudentId,
            Date      = date.ToString("yyyy-MM-dd"),
            Status    = statusResult.Value.ToString()
        }, cancellationToken);

        // 8. Return
        return Result<RecordAttendanceResponse>.Success(
            new RecordAttendanceResponse(id.Value, statusResult.Value.ToString()));
    }
}
