using Analytics.Application.Dashboard;
using BuildingBlocks.Application.Messaging;

namespace Analytics.Application.Dashboard.GetDashboard;

/// <summary>Query for the aggregated analytics dashboard.</summary>
public sealed record GetDashboardQuery : IQuery<DashboardDto>;
