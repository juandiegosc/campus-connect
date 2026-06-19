using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.GraduateStudent;

/// <summary>
/// HTTP-triggered command to graduate a student (Phase 4 — ADR-066/067).
/// Graduate is TERMINAL: Active|Suspended → Graduated. Already-Graduated → 409 (ADR-066).
/// CRITICAL (Gotcha 16): implements ICommand (not IRequest&lt;T&gt;) to activate UnitOfWorkBehavior,
/// so the StudentStatusUpdated outbox INSERT commits atomically with the student UPDATE.
/// </summary>
public sealed record GraduateStudentCommand(string StudentId) : ICommand;
