using Attendance.Application.Students.Shared;
using BuildingBlocks.Application.Messaging;

namespace Attendance.Application.Students.GetStudentHistory;

/// <summary>
/// Query to get a student's attendance and incident history (REQ-AT1-17, REQ-AT1-26).
/// </summary>
public sealed record GetStudentHistoryQuery(string StudentId) : IQuery<StudentHistoryDto>;
