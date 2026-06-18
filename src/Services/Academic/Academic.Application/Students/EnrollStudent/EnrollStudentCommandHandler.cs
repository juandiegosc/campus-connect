using Academic.Application.Abstractions;
using Academic.Domain.Students;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Contracts.Events;
using MediatR;

namespace Academic.Application.Students.EnrollStudent;

/// <summary>
/// Handles the EnrollStudentCommand.
/// Flow:
///   1. Validate DocumentId format → Error.Validation on failure
///   2. Check duplicate DocumentId → Error.Conflict if exists
///   3. Create GuardianContact VO → Error.Validation on failure
///   4. Generate StudentId and EnrollmentId via IUlidGenerator
///   5. Create Student aggregate (raises StudentEnrolledDomainEvent)
///   6. Persist via IStudentRepository.AddAsync (no SaveChanges yet)
///   7. Enqueue StudentEnrolled via IIntegrationEventPublisher (Application port,
///      MassTransit-backed in Infrastructure). The outbox interceptor defers the
///      OutboxMessage INSERT until SaveChangesAsync runs.
///   8. Return EnrollStudentResponse
/// Then UnitOfWorkBehavior calls SaveChangesAsync which commits:
///   - INSERT students row
///   - INSERT OutboxMessage row (MassTransit outbox interceptor)
/// Both in the same EF Core transaction — atomic guarantee (ESC-42).
/// </summary>
public sealed class EnrollStudentCommandHandler(
    IStudentRepository           repo,
    IUlidGenerator               ulid,
    IIntegrationEventPublisher   publisher)
    : IRequestHandler<EnrollStudentCommand, Result<EnrollStudentResponse>>
{
    public async Task<Result<EnrollStudentResponse>> Handle(
        EnrollStudentCommand command,
        CancellationToken    cancellationToken)
    {
        // 1. Validate DocumentId format
        var (documentId, docError) = DocumentId.TryCreate(command.DocumentId);
        if (documentId is null)
            return Result<EnrollStudentResponse>.Failure(Error.Validation(
                "document_id.invalid_format", docError!));

        // 2. Duplicate check
        var exists = await repo.ExistsByDocumentIdAsync(documentId, cancellationToken);
        if (exists)
            return Result<EnrollStudentResponse>.Failure(Error.Conflict(
                "student.document_id_duplicate",
                $"A student with DocumentId '{command.DocumentId}' already exists."));

        // 3. Create GuardianContact VO
        var (guardian, guardianError) = GuardianContact.TryCreate(command.GuardianName, command.GuardianEmail);
        if (guardian is null)
            return Result<EnrollStudentResponse>.Failure(Error.Validation(
                "guardian.invalid", guardianError!));

        // 4. Generate IDs
        var now          = DateTimeOffset.UtcNow;
        var studentId    = StudentId.Parse(ulid.NewId(now));
        var enrollmentId = ulid.NewId(now.AddTicks(1));

        // 5. Create aggregate (raises StudentEnrolledDomainEvent internally)
        var student = Student.Create(
            studentId,
            command.FullName,
            documentId,
            command.Grade,
            command.SchoolId,
            guardian,
            enrollmentId,
            now.UtcDateTime);

        // 6. Track in EF (no SaveChanges yet)
        await repo.AddAsync(student, cancellationToken);

        // 7. Enqueue integration event via Application port.
        // Infrastructure implementation wraps MassTransit IPublishEndpoint; the EF Core outbox
        // interceptor defers the OutboxMessage INSERT until UoW calls SaveChangesAsync, so
        // INSERT students + INSERT OutboxMessage commit in the SAME EF transaction.
        await publisher.PublishAsync(
            new StudentEnrolled
            {
                StudentId    = studentId.Value,
                EnrollmentId = enrollmentId,
                SchoolId     = command.SchoolId,
                Grade        = command.Grade,
                FullName     = command.FullName
            },
            cancellationToken);

        // 8. Return response (UnitOfWorkBehavior commits atomically after this returns)
        return Result<EnrollStudentResponse>.Success(
            new EnrollStudentResponse(studentId.Value, enrollmentId, "Active"));
    }
}
