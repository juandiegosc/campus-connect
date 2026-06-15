using System.Diagnostics;
using BuildingBlocks.Application.Correlation;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICorrelationContext correlationContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = correlationContext.CorrelationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestName", requestName))
        {
            logger.LogInformation("Handling {RequestName} [CorrelationId: {CorrelationId}]",
                requestName, correlationId);

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await next();
                sw.Stop();
                logger.LogInformation("Handled {RequestName} in {Elapsed}ms [CorrelationId: {CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, correlationId);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex,
                    "Error handling {RequestName} after {Elapsed}ms [CorrelationId: {CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, correlationId);
                throw;
            }
        }
    }
}
