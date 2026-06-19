using BuildingBlocks.Application.Messaging;

namespace Attendance.Application.Attendance.RecordAttendance;

/// <summary>
/// Command to record student attendance (REQ-AT1-17).
/// ICommand trigger activates UnitOfWorkBehavior (REQ-AT1-18).
/// </summary>
public sealed record RecordAttendanceCommand(
    string StudentId,
    string Date,
    string Status) : ICommand<RecordAttendanceResponse>;
