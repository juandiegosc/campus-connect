using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Infrastructure.Correlation;

public static class CorrelationApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="CorrelationIdMiddleware"/> to the request pipeline.
    /// Call this before UseRouting / UseAuthentication.
    /// </summary>
    public static IApplicationBuilder UseCampusConnectCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
