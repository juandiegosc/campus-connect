using Academic.Application.Abstractions;
using Academic.Application.Students.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academic.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads student-related events from the MassTransit EF Core outbox table (ADR-036, R10).
/// Uses Npgsql directly to avoid EF Core query translation issues with raw SQL projections.
/// Table: "OutboxMessage" (MassTransit PascalCase convention — verified in InitialAcademic migration).
/// Columns: MessageType (text), SentTime (timestamptz), CorrelationId (uuid).
/// Outbox grows indefinitely for local-only project (Q4 constraint).
/// </summary>
internal sealed class OutboxEventReader(AcademicDbContext ctx) : IOutboxEventReader
{
    public async Task<IReadOnlyList<StudentEventDto>> GetEventsForStudentAsync(
        string studentId, CancellationToken ct = default)
    {
        var results = new List<StudentEventDto>();
        var pattern = $"%{studentId}%";

        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ""MessageType"", ""SentTime"", CAST(""CorrelationId"" AS text)
            FROM ""OutboxMessage""
            WHERE (""MessageType"" LIKE '%StudentEnrolled%' OR ""MessageType"" LIKE '%StudentStatusUpdated%')
              AND ""Body"" LIKE @pattern
            ORDER BY ""SentTime"" ASC";

        var param = cmd.CreateParameter();
        param.ParameterName = "@pattern";
        param.Value = pattern;
        cmd.Parameters.Add(param);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new StudentEventDto(
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? DateTime.UtcNow : reader.GetDateTime(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        return results;
    }
}
