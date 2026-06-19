using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR with the Attendance.Application assembly (handlers + validators),
    /// FluentValidation validators, and the shared MediatR pipeline behaviors
    /// (Logging → Validation → UnitOfWork) from BuildingBlocks.Application.
    /// </summary>
    public static IServiceCollection AddAttendanceApplication(this IServiceCollection services)
    {
        services.AddCampusConnectApplication(
            typeof(BuildingBlocks.Application.DependencyInjection).Assembly,
            typeof(DependencyInjection).Assembly);

        return services;
    }
}
