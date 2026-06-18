using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.RegisterObligation;

/// <summary>
/// Handles RegisterObligationCommand.
/// Flow:
///   1. Generate ObligationId via IUlidGenerator
///   2. Create Obligation.Register(...) — domain validates invariants
///   3. repo.AddAsync (EF tracks; NO SaveChanges)
///   4. Return Result.Success (UnitOfWorkBehavior commits after this returns)
/// No integration event published here — register has no outbox message.
/// </summary>
public sealed class RegisterObligationCommandHandler(
    IObligationRepository repo,
    IUlidGenerator        ulid)
    : IRequestHandler<RegisterObligationCommand, Result<RegisterObligationResponse>>  // ICommand<TResp> = IRequest<Result<TResp>>
{
    public async Task<Result<RegisterObligationResponse>> Handle(
        RegisterObligationCommand command,
        CancellationToken         cancellationToken)
    {
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
