using Academic.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.GetStudents;

/// <summary>Query to retrieve a paginated list of students with optional filters.</summary>
public sealed record GetStudentsQuery(
    int     Page,
    int     PageSize,
    string? Grade,
    string? Search
) : IQuery<PagedList<StudentListItemDto>>;
