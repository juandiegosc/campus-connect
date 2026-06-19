using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.MarkOverdue;

/// <summary>
/// HTTP-triggered command to mark a student's financial status as Overdue (Phase 3).
/// CRITICAL (Gotcha 16): implements ICommand (not IRequest&lt;T&gt;) to activate UnitOfWorkBehavior,
/// so the StudentStatusUpdated outbox INSERT commits atomically with the student UPDATE.
/// </summary>
public sealed record MarkStudentOverdueCommand(string StudentId) : ICommand;
