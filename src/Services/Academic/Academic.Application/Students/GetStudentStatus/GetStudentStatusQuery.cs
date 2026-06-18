using Academic.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.GetStudentStatus;

/// <summary>Query to retrieve a student's current academic and financial status.</summary>
public sealed record GetStudentStatusQuery(string StudentId)
    : IQuery<StudentStatusDto>;
