using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Application.Students.Shared;

namespace Payments.Application.Students.GetStudents;

/// <summary>
/// Handles GetStudentsQuery — returns a paginated list of StudentReplica items.
/// Handler is IRequestHandler&lt;GetStudentsQuery, Result&lt;PagedList&lt;StudentReplicaItemDto&gt;&gt;&gt;
/// because IQuery&lt;T&gt; expands to IRequest&lt;Result&lt;T&gt;&gt; (Gotcha 25, ADR-R7).
/// </summary>
public sealed class GetStudentsQueryHandler(IStudentReplicaRepository repo)
    : IRequestHandler<GetStudentsQuery, Result<PagedList<StudentReplicaItemDto>>>
{
    public async Task<Result<PagedList<StudentReplicaItemDto>>> Handle(
        GetStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, total) = await repo.GetPagedAsync(
            query.Page, query.PageSize, query.Grade, query.Search, cancellationToken);

        return Result<PagedList<StudentReplicaItemDto>>.Success(
            new PagedList<StudentReplicaItemDto>(items, total));
    }
}
