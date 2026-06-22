using Analytics.Application.Abstractions;
using Analytics.Application.Dashboard;
using BuildingBlocks.Application.Common;
using MediatR;

namespace Analytics.Application.Dashboard.GetDashboard;

/// <summary>Handler for GetDashboardQuery.</summary>
public sealed class GetDashboardQueryHandler(IAnalyticsRepository repo)
    : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    public async Task<Result<DashboardDto>> Handle(
        GetDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var dashboard = await repo.GetDashboardAsync(cancellationToken);
        return Result<DashboardDto>.Success(dashboard);
    }
}
