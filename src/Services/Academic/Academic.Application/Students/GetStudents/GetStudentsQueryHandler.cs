using Academic.Application.Abstractions;
using Academic.Application.Students.Shared;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Academic.Application.Students.GetStudents;

public sealed class GetStudentsQueryHandler(IStudentRepository repo)
    : IRequestHandler<GetStudentsQuery, Result<PagedList<StudentListItemDto>>>
{
    public async Task<Result<PagedList<StudentListItemDto>>> Handle(
        GetStudentsQuery  query,
        CancellationToken cancellationToken)
    {
        var (items, total) = await repo.GetPagedAsync(
            query.Page, query.PageSize, query.Grade, query.Search, cancellationToken);

        var dtos = items.Select(s => new StudentListItemDto(
            s.Id.Value,
            s.FullName,
            s.Grade,
            s.AcademicStatus.ToString(),
            s.FinancialStatus.ToString())).ToList();

        return Result<PagedList<StudentListItemDto>>.Success(new PagedList<StudentListItemDto>(dtos, total));
    }
}
