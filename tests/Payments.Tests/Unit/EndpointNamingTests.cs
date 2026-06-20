using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Infrastructure;
using Payments.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Payments.Tests.Unit;

/// <summary>
/// Regression for messaging-endpoint-isolation (ADR-076, REQ-MEI-01..03).
/// Payments must prefix its consumer queues so StudentEnrolledConsumer does NOT share
/// a queue with Attendance's homonymous consumer (competing-consumers bug, verified live).
/// </summary>
public sealed class EndpointNamingTests
{
    private static IEndpointNameFormatter BuildFormatter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentsDb"] =
                    "Host=localhost;Port=5435;Database=payments_db;Username=campus;Password=campus"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddPaymentsInfrastructure(config);

        return services.BuildServiceProvider().GetRequiredService<IEndpointNameFormatter>();
    }

    [Fact]
    public void PaymentsBus_PrefixesStudentEnrolledQueueWithServiceName()
    {
        BuildFormatter().Consumer<StudentEnrolledConsumer>()
            .Should().Be("payments-student-enrolled",
                "REQ-MEI-02: distinto de attendance-student-enrolled → ambos reciben el evento (fan-out)");
    }

    [Fact]
    public void PaymentsBus_PrefixesStudentStatusUpdatedQueueWithServiceName()
    {
        BuildFormatter().Consumer<StudentStatusUpdatedConsumer>()
            .Should().Be("payments-student-status-updated", "REQ-MEI-03");
    }
}
