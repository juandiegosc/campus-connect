using BuildingBlocks.Application.Common;
using FluentValidation;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
        var error = Error.Validation("VALIDATION_FAILED", errorMessage);

        // Result<T>.Failure or Result.Failure — use reflection to construct the right type
        if (typeof(TResponse).IsGenericType)
        {
            var failureMethod = typeof(TResponse)
                .GetMethod("Failure",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    [typeof(Error)]);

            if (failureMethod is not null)
                return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        return (TResponse)Result.Failure(error);
    }
}
