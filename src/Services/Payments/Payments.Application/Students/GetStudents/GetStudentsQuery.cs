using BuildingBlocks.Application.Messaging;
using Payments.Application.Students.Shared;

namespace Payments.Application.Students.GetStudents;

/// <summary>
/// Query to retrieve a paginated, optionally-filtered list of student replicas.
/// SINGLE-wrap: IQuery&lt;PagedList&lt;T&gt;&gt; (NOT IQuery&lt;Result&lt;PagedList&lt;T&gt;&gt;&gt;).
/// IQuery&lt;T&gt; expands to IRequest&lt;Result&lt;T&gt;&gt; — handler returns Result&lt;PagedList&lt;T&gt;&gt;.
/// Mirror of Academic's GetStudentsQuery shape exactly (Gotcha 25, ADR-R7).
/// </summary>
public sealed record GetStudentsQuery(
    int     Page,
    int     PageSize,
    string? Grade,
    string? Search
) : IQuery<PagedList<StudentReplicaItemDto>>;
