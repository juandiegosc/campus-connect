using Analytics.Application.Abstractions;
using Analytics.Application.Events;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Analytics.Application.Events.GetEvents;

/// <summary>Handler for GetEventsQuery.</summary>
public sealed class GetEventsQueryHandler(IAnalyticsRepository repo)
    : IRequestHandler<GetEventsQuery, Result<IReadOnlyList<EventLogDto>>>
{
    public async Task<Result<IReadOnlyList<EventLogDto>>> Handle(
        GetEventsQuery query,
        CancellationToken cancellationToken)
    {
        var take = query.Take is <= 0 or > 500 ? 100 : query.Take;
        var list = await repo.GetRecentEventsAsync(take, cancellationToken);
        return Result<IReadOnlyList<EventLogDto>>.Success(list);
    }
}
