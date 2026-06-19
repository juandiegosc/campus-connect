using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academic.Application.Students.EnrollStudent;
using Academic.Infrastructure.Persistence;
using Academic.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Academic.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/academic/students/{id}/mark-overdue (Phase 3, ADR-063/064).
/// Shares the Postgres container via [Collection("AcademicPostgres")]; isolation via unique DocumentIds.
/// ESC-64..ESC-65.
/// </summary>
[Collection("AcademicPostgres")]
public sealed class MarkStudentOverdueIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> EnrollStudentAsync(string documentId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var body = new
        {
            fullName      = "Overdue Test Student",
            documentId    = documentId,
            grade         = "9no EGB",
            schoolId      = "SCH-001",
            guardianName  = "Overdue Guardian",
            guardianEmail = "overdue-guardian@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/academic/students", body);
        response.EnsureSuccessStatusCode();
        var enrolled = await response.Content.ReadFromJsonAsync<EnrollStudentResponse>();
        return enrolled!.StudentId;
    }

    private static async Task<string?> QueryFinancialStatusAsync(
        AcademicWebApplicationFactory fac, string studentId)
    {
        using var scope = fac.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT financial_status FROM students WHERE student_id = '{studentId}'";
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    /// <summary>ESC-64 — Happy path: POST mark-overdue → 200, status Overdue, StudentStatusUpdated published.</summary>
    [Fact]
    public async Task MarkOverdue_PendingStudent_Returns200_SetsOverdue_PublishesStatus()
    {
        var studentId = await EnrollStudentAsync("P3A0000001");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Direccion"));

        var response = await _client.PostAsync($"/api/academic/students/{studentId}/mark-overdue", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await QueryFinancialStatusAsync(factory, studentId);
        status.Should().Be("Overdue", "ESC-64: financial_status must transition to Overdue");

        // StudentStatusUpdated is staged in the outbox atomically with the status UPDATE (Gotcha 28).
        // Assert the OutboxMessage row via raw SQL (mirrors EnrollStudent integration pattern). The
        // HTTP path stages but does not promptly deliver to the in-memory harness bus, so
        // harness.Published is unreliable here (Gotcha 30).
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentStatusUpdated%' AND \"Body\" LIKE '%{studentId}%'";
        var outboxCount = (long)(await cmd.ExecuteScalarAsync())!;
        outboxCount.Should().BeGreaterThan(0,
            "ESC-64: StudentStatusUpdated must be staged in the outbox atomically with the status change");
    }

    /// <summary>ESC-65 — No token → 401.</summary>
    [Fact]
    public async Task MarkOverdue_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/mark-overdue", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>ESC-65 — Wrong role (Docente) → 403.</summary>
    [Fact]
    public async Task MarkOverdue_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/mark-overdue", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
