using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.GetObligationById;

public sealed class GetObligationByIdQueryHandler(IObligationRepository repo)
    : IRequestHandler<GetObligationByIdQuery, Result<ObligationDetailDto>>
{
    public async Task<Result<ObligationDetailDto>> Handle(
        GetObligationByIdQuery query,
        CancellationToken      cancellationToken)
    {
        ObligationId id;
        try { id = ObligationId.Parse(query.ObligationId); }
        catch { return Result<ObligationDetailDto>.Failure(Error.NotFound("obligation.not_found", $"Obligation '{query.ObligationId}' not found.")); }

        var obligation = await repo.GetByIdAsync(id, cancellationToken);
        if (obligation is null)
            return Result<ObligationDetailDto>.Failure(Error.NotFound("obligation.not_found", $"Obligation '{query.ObligationId}' not found."));

        var paymentDto = obligation.Payment is not null
            ? new PaymentDto(
                obligation.Payment.Id.Value,
                obligation.Payment.Method.ToString(),
                obligation.Payment.Reference,
                obligation.Payment.ConfirmedAt)
            : null;

        return Result<ObligationDetailDto>.Success(new ObligationDetailDto(
            obligation.Id.Value,
            obligation.StudentId,
            obligation.Concept,
            obligation.Amount,
            obligation.DueDate,
            obligation.SchoolId,
            obligation.Status.ToString(),
            paymentDto));
    }
}
