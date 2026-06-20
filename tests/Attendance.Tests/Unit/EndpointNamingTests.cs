using Attendance.Infrastructure;
using Attendance.Infrastructure.Messaging.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Attendance.Tests.Unit;

/// <summary>
/// Regression for messaging-endpoint-isolation (ADR-076, REQ-MEI-01..02).
/// Each service MUST prefix its receive-endpoint (queue) names so that two services
/// hosting a same-named consumer (StudentEnrolledConsumer in BOTH Payments and Attendance)
/// do NOT collide on one shared queue and become competing consumers.
/// </summary>
public sealed class EndpointNamingTests
{
    [Fact]
    public void AttendanceBus_PrefixesConsumerQueueWithServiceName()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AttendanceDb"] =
                    "Host=localhost;Port=5436;Database=attendance_db;Username=campus;Password=campus"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddAttendanceInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var formatter = provider.GetRequiredService<IEndpointNameFormatter>();

        formatter.Consumer<StudentEnrolledConsumer>()
            .Should().Be("attendance-student-enrolled",
                "REQ-MEI-02: la cola debe ir prefijada por servicio para no colisionar con payments-student-enrolled");
    }
}
