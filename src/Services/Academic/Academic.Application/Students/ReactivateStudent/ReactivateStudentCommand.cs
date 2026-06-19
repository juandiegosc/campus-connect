using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.ReactivateStudent;

/// <summary>
/// HTTP-triggered command to reactivate a suspended student's academic status (Phase 4 — ADR-067).
/// CRITICAL (Gotcha 16): implements ICommand (not IRequest&lt;T&gt;) to activate UnitOfWorkBehavior,
/// so the StudentStatusUpdated outbox INSERT commits atomically with the student UPDATE.
/// </summary>
public sealed record ReactivateStudentCommand(string StudentId) : ICommand;
