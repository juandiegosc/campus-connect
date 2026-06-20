using Attendance.Application.Abstractions;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Attendance.Infrastructure.Messaging.Consumers;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.Repositories;
using Attendance.Infrastructure.Services;

namespace Attendance.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Attendance infrastructure:
    /// - AttendanceDbContext (connection string key "AttendanceDb" with fallback "Default")
    /// - IUnitOfWork resolved from AttendanceDbContext
    /// - IAttendanceRecordRepository
    /// - IIncidentRepository
    /// - IStudentReplicaRepository
    /// - IUlidGenerator (singleton)
    /// - StudentEnrolledConsumer (registered BEFORE AddCampusConnectMassTransit per ADR-042)
    /// CRITICAL (ADR-042): AddConsumer must come BEFORE AddCampusConnectMassTransit so
    /// topology configuration sees all consumers when wiring endpoints.
    /// </summary>
    public static IServiceCollection AddAttendanceInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        var connStr = configuration.GetConnectionString("AttendanceDb")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:AttendanceDb (or ConnectionStrings:Default) is required for Attendance service.");

        services.AddDbContext<AttendanceDbContext>(opts => opts.UseNpgsql(connStr));

        // IUnitOfWork resolved from the same AttendanceDbContext instance
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AttendanceDbContext>());

        services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IStudentReplicaRepository, StudentReplicaRepository>();

        // ULID generator is stateless — singleton is safe (ADR-036)
        services.AddSingleton<IUlidGenerator, UlidGenerator>();

        // CRITICAL (ADR-038 + ADR-042): AddConsumer BEFORE AddCampusConnectMassTransit
        services.AddMassTransit(cfg =>
        {
            // StudentEnrolledConsumer — BEFORE topology wiring (ADR-042 R1)
            cfg.AddConsumer<StudentEnrolledConsumer>();

            cfg.AddCampusConnectMassTransit<AttendanceDbContext>(configuration, "attendance");  // ADR-076: queue prefix
        });

        return services;
    }
}
