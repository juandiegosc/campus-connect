using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.RegisterObligation;

/// <summary>
/// Handles RegisterObligationCommand.
/// Flow:
///   1. Guard: ExistsAsync on StudentReplica — reject unknown StudentId with Error.Validation (ADR-056)
///   2. Generate ObligationId via IUlidGenerator
///   3. Create Obligation.Register(...) — domain validates invariants
///   4. repo.AddAsync (EF tracks; NO SaveChanges)
///   5. Return Result.Success (UnitOfWorkBehavior commits after this returns)
/// No integration event published here — register has no outbox message.
///
/// ADR-056: guard returns Error.Validation (NOT Error.NotFound) so MapError yields HTTP 400.
/// </summary>
public sealed class RegisterObligationCommandHandler(
    IObligationRepository     repo,
    IStudentReplicaRepository students,
    IUlidGenerator            ulid)
    : IRequestHandler<RegisterObligationCommand, Result<RegisterObligationResponse>>  // ICommand<TResp> = IRequest<Result<TResp>>
{
    public async Task<Result<RegisterObligationResponse>> Handle(
        RegisterObligationCommand command,
        CancellationToken         cancellationToken)
    {
        // Phase 2: existence guard — BEFORE creating the obligation (ADR-056, REQ-PM2-04).
        // IMPORTANT: use Error.Validation NOT Error.NotFound.
        // Payments.API MapError: Validation→400, NotFound→404. The locked contract is HTTP 400.
        if (!await students.ExistsAsync(command.StudentId, cancellationToken))
            return Result<RegisterObligationResponse>.Failure(
                Error.Validation(
                    "student.not_found",
                    $"Student {command.StudentId} is not known to Payments."));

        var now = DateTimeOffset.UtcNow;
        var id  = ObligationId.Parse(ulid.NewId(now));

        var obligation = Obligation.Register(
            id,
            command.StudentId,
            command.Concept,
            command.Amount,
            command.DueDate,
            "SCH-001",      // TODO multi-tenant
            now.UtcDateTime);

        await repo.AddAsync(obligation, cancellationToken);

        return Result<RegisterObligationResponse>.Success(
            new RegisterObligationResponse(id.Value, "Pending"));
    }
}
