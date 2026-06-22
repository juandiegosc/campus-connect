using Analytics.Application.Events;
using BuildingBlocks.Application.Messaging;

namespace Analytics.Application.Events.GetEvents;

/// <summary>Query for the processed-events log.</summary>
public sealed record GetEventsQuery(int Take = 100) : IQuery<IReadOnlyList<EventLogDto>>;
