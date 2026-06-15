using BuildingBlocks.Application.Common;
using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>Marker interface for all queries (used as constraint in behaviors).</summary>
public interface IBaseQuery;

/// <summary>Query that returns a typed Result.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>, IBaseQuery;
