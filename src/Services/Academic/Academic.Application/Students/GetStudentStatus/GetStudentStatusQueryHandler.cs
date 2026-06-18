using Academic.Application.Abstractions;
using Academic.Application.Students.Shared;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Academic.Application.Students.GetStudentStatus;

public sealed class GetStudentStatusQueryHandler(IStudentRepository repo)
    : IRequestHandler<GetStudentStatusQuery, Result<StudentStatusDto>>
{
    public async Task<Result<StudentStatusDto>> Handle(
        GetStudentStatusQuery query,
        CancellationToken     cancellationToken)
    {
        StudentId studentId;
        try { studentId = StudentId.Parse(query.StudentId); }
        catch { return Result<StudentStatusDto>.Failure(Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found.")); }

        var student = await repo.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
            return Result<StudentStatusDto>.Failure(Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found."));

        return Result<StudentStatusDto>.Success(new StudentStatusDto(
            student.Id.Value,
            Exists: true,
            student.AcademicStatus.ToString(),
            student.FinancialStatus.ToString()));
    }
}
