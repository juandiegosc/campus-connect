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
/// Integration tests for POST /api/academic/students/{id}/reactivate (Phase 4, ADR-067/068).
/// Shares the Postgres container via [Collection("AcademicPostgres")]; isolation via unique DocumentIds (prefix: P4R).
/// ESC-77..ESC-82.
/// Reactivate happy path drives suspend via HTTP endpoint (true e2e — not reflection/DB seed).
/// </summary>
[Collection("AcademicPostgres")]
public sealed class ReactivateStudentIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> EnrollStudentAsync(string documentId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var body = new
        {
            fullName      = "Reactivate Test Student",
            documentId    = documentId,
            grade         = "9no EGB",
            schoolId      = "SCH-001",
            guardianName  = "Reactivate Guardian",
            guardianEmail = "reactivate-guardian@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/academic/students", body);
        response.EnsureSuccessStatusCode();
        var enrolled = await response.Content.ReadFromJsonAsync<EnrollStudentResponse>();
        return enrolled!.StudentId;
    }

    private static async Task<string?> QueryAcademicStatusAsync(
        AcademicWebApplicationFactory fac, string studentId)
    {
        using var scope = fac.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT academic_status FROM students WHERE student_id = '{studentId}'";
        return (await cmd.ExecuteScalarAsync())?.ToString();
    }

    /// <summary>
    /// ESC-77 — Happy path: enroll → suspend (HTTP) → reactivate → 200, academic_status=Active, outbox row.
    /// Uses HTTP suspend endpoint to set up Suspended state (true e2e per design spec).
    /// </summary>
    [Fact]
    public async Task Reactivate_SuspendedStudent_Returns200_SetsActive_PublishesStatus()
    {
        var studentId = await EnrollStudentAsync("P4R0000001");

        // Drive suspend via HTTP (true e2e — not reflection)
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));
        var suspendResponse = await _client.PostAsync($"/api/academic/students/{studentId}/suspend", null);
        suspendResponse.EnsureSuccessStatusCode();

        // Now reactivate
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));
        var response = await _client.PostAsync($"/api/academic/students/{studentId}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await QueryAcademicStatusAsync(factory, studentId);
        status.Should().Be("Active", "ESC-77: academic_status must transition back to Active");

        // Assert outbox row for the reactivate event
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentStatusUpdated%' AND \"Body\" LIKE '%{studentId}%'";
        var outboxCount = (long)(await cmd.ExecuteScalarAsync())!;
        outboxCount.Should().BeGreaterThan(0,
            "ESC-77: StudentStatusUpdated must be staged in the outbox after reactivation");
    }

    /// <summary>ESC-81 — No token → 401.</summary>
    [Fact]
    public async Task Reactivate_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>ESC-82 — Docente role → 403.</summary>
    [Fact]
    public async Task Reactivate_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
