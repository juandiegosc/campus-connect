using BuildingBlocks.Application.Common;
using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>Marker interface for all commands (used as constraint in UnitOfWorkBehavior).</summary>
public interface IBaseCommand;

/// <summary>Command that returns a non-value Result.</summary>
public interface ICommand : IRequest<Result>, IBaseCommand;

/// <summary>Command that returns a typed Result.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;
