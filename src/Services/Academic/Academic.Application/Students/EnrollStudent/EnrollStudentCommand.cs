using BuildingBlocks.Application.Messaging;

namespace Academic.Application.Students.EnrollStudent;

/// <summary>
/// Command to enroll a new student.
/// CRITICAL (Gotcha 16): Implements ICommand&lt;EnrollStudentResponse&gt; which resolves to
/// IRequest&lt;Result&lt;EnrollStudentResponse&gt;&gt;, activating UnitOfWorkBehavior.
/// Using IRequest&lt;T&gt; directly would skip UnitOfWork and the outbox INSERT would not commit atomically.
/// </summary>
public sealed record EnrollStudentCommand(
    string FullName,
    string DocumentId,
    string Grade,
    string SchoolId,
    string GuardianName,
    string GuardianEmail
) : ICommand<EnrollStudentResponse>;
