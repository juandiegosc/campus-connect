using BuildingBlocks.Application.Common;
using MediatR;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Application.Obligations.GetObligations;

public sealed class GetObligationsQueryHandler(IObligationRepository repo)
    : IRequestHandler<GetObligationsQuery, Result<IReadOnlyList<ObligationListItemDto>>>
{
    public async Task<Result<IReadOnlyList<ObligationListItemDto>>> Handle(
        GetObligationsQuery query,
        CancellationToken   cancellationToken)
    {
        ObligationStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ObligationStatus>(query.Status, ignoreCase: true, out var parsed))
                return Result<IReadOnlyList<ObligationListItemDto>>.Failure(Error.Validation(
                    "obligation_status.invalid",
                    $"Status '{query.Status}' is not valid. Must be Pending or Confirmed."));
            status = parsed;
        }

        var items = await repo.GetByStatusAsync(status, cancellationToken);

        IReadOnlyList<ObligationListItemDto> dtos = items
            .Select(o => new ObligationListItemDto(
                o.Id.Value,
                o.StudentId,
                o.Concept,
                o.Amount,
                o.DueDate,
                o.Status.ToString()))
            .ToList();

        return Result<IReadOnlyList<ObligationListItemDto>>.Success(dtos);
    }
}
