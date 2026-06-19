using Attendance.Application.Abstractions;
using Attendance.Application.Students.Shared;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Attendance.Application.Students.GetStudents;

/// <summary>Handler for GetStudentsQuery.</summary>
public sealed class GetStudentsQueryHandler(IStudentReplicaRepository students)
    : IRequestHandler<GetStudentsQuery, Result<IReadOnlyList<StudentReplicaDto>>>
{
    public async Task<Result<IReadOnlyList<StudentReplicaDto>>> Handle(
        GetStudentsQuery  query,
        CancellationToken cancellationToken)
    {
        var list = await students.GetAllAsync(cancellationToken);
        return Result<IReadOnlyList<StudentReplicaDto>>.Success(list);
    }
}
