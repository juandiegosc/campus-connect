using Attendance.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Attendance.Application.Students.GetStudents;

/// <summary>
/// Query to get all student replicas (REQ-AT1-17).
/// IQuery single-wrap: returns Result(IReadOnlyList(StudentReplicaDto)) (REQ-AT1-17).
/// </summary>
public sealed record GetStudentsQuery : IQuery<IReadOnlyList<StudentReplicaDto>>;
