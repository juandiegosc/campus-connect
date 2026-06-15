using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Messaging;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class UnitOfWorkBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }
}
