using BuildingBlocks.Application.Correlation;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Correlation;

public sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public string CorrelationId =>
        httpContextAccessor.HttpContext?.Items["CorrelationId"] as string
        ?? Guid.NewGuid().ToString("N");
}
