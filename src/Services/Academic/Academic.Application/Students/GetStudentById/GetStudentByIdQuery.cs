using Academic.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.GetStudentById;

/// <summary>Query to retrieve a single student's full details by ID.</summary>
public sealed record GetStudentByIdQuery(string StudentId)
    : IQuery<StudentDetailDto>;
