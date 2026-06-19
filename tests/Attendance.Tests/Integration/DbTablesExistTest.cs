using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Attendance.Infrastructure.Persistence;
using Xunit;

namespace Attendance.Tests.Integration;

/// <summary>
/// Smoke test: verify all 6 expected tables were created by InitialAttendance migration (REQ-AT1-32).
/// attendance_records, incidents, student_replicas, OutboxMessage, OutboxState, InboxState.
/// </summary>
[Collection("AttendancePostgres")]
public sealed class DbTablesExistTest(AttendanceWebApplicationFactory factory)
    : IClassFixture<AttendanceWebApplicationFactory>
{
    private static readonly string[] ExpectedTables =
    [
        "attendance_records",
        "incidents",
        "student_replicas",
        "OutboxMessage",
        "OutboxState",
        "InboxState"
    ];

    [Fact]
    public async Task AllSixTablesShouldExistAfterMigration()
    {
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        foreach (var table in ExpectedTables)
        {
            await using var cmd = conn.CreateCommand();
            // Use pg_tables for public schema — works for both standard and MassTransit tables
            cmd.CommandText = $"""
                SELECT COUNT(1) FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = '{table}'
                """;
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            count.Should().Be(1, $"table '{table}' must exist after InitialAttendance migration");
        }
    }
}
