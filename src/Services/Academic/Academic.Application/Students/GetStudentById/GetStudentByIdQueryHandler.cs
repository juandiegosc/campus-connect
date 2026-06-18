using Academic.Application.Abstractions;
using Academic.Application.Students.Shared;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Academic.Application.Students.GetStudentById;

public sealed class GetStudentByIdQueryHandler(IStudentRepository repo)
    : IRequestHandler<GetStudentByIdQuery, Result<StudentDetailDto>>
{
    public async Task<Result<StudentDetailDto>> Handle(
        GetStudentByIdQuery query,
        CancellationToken   cancellationToken)
    {
        StudentId studentId;
        try { studentId = StudentId.Parse(query.StudentId); }
        catch { return Result<StudentDetailDto>.Failure(Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found.")); }

        var student = await repo.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
            return Result<StudentDetailDto>.Failure(Error.NotFound("student.not_found", $"Student '{query.StudentId}' not found."));

        return Result<StudentDetailDto>.Success(new StudentDetailDto(
            student.Id.Value,
            student.FullName,
            student.DocumentId.Value,
            student.Grade,
            student.SchoolId,
            student.AcademicStatus.ToString(),
            student.FinancialStatus.ToString(),
            new GuardianDto(student.Guardian.Name, student.Guardian.Email)));
    }
}
