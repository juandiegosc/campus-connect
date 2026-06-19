using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.SuspendStudent;

/// <summary>
/// HTTP-triggered command to suspend a student's academic status (Phase 4 — ADR-067).
/// CRITICAL (Gotcha 16): implements ICommand (not IRequest&lt;T&gt;) to activate UnitOfWorkBehavior,
/// so the StudentStatusUpdated outbox INSERT commits atomically with the student UPDATE.
/// </summary>
public sealed record SuspendStudentCommand(string StudentId) : ICommand;
