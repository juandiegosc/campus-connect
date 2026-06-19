using Attendance.Application.Abstractions;
using Attendance.Application.Students.Shared;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Attendance.Application.Students.GetStudentHistory;

/// <summary>
/// Handler for GetStudentHistoryQuery.
/// Returns 404 if student not in replica (ASSUMPTION-SPEC-02, REQ-AT1-44, ESC-AT-19).
/// </summary>
public sealed class GetStudentHistoryQueryHandler(
    IStudentReplicaRepository    students,
    IAttendanceRecordRepository  attendanceRepo,
    IIncidentRepository          incidentRepo)
    : IRequestHandler<GetStudentHistoryQuery, Result<StudentHistoryDto>>
{
    public async Task<Result<StudentHistoryDto>> Handle(
        GetStudentHistoryQuery query,
        CancellationToken      cancellationToken)
    {
        if (!await students.ExistsAsync(query.StudentId, cancellationToken))
            return Result<StudentHistoryDto>.Failure(
                Error.NotFound("student.not_found",
                    $"Student {query.StudentId} not found."));

        var attendance = await attendanceRepo.GetByStudentAsync(query.StudentId, cancellationToken);
        var incidents  = await incidentRepo.GetByStudentAsync(query.StudentId, cancellationToken);

        return Result<StudentHistoryDto>.Success(
            new StudentHistoryDto(attendance, incidents));
    }
}
