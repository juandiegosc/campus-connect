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
/// Integration tests for POST /api/academic/students/{id}/suspend (Phase 4, ADR-067/068).
/// Shares the Postgres container via [Collection("AcademicPostgres")]; isolation via unique DocumentIds (prefix: P4S).
/// ESC-70..ESC-75.
/// </summary>
[Collection("AcademicPostgres")]
public sealed class SuspendStudentIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> EnrollStudentAsync(string documentId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var body = new
        {
            fullName      = "Suspend Test Student",
            documentId    = documentId,
            grade         = "9no EGB",
            schoolId      = "SCH-001",
            guardianName  = "Suspend Guardian",
            guardianEmail = "suspend-guardian@example.com"
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

    /// <summary>ESC-70 — Happy path (Direccion token): POST /suspend → 200, academic_status=Suspended, outbox row.</summary>
    [Fact]
    public async Task Suspend_ActiveStudent_Returns200_SetsSuspended_PublishesStatus()
    {
        var studentId = await EnrollStudentAsync("P4S0000001");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Direccion"));

        var response = await _client.PostAsync($"/api/academic/students/{studentId}/suspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await QueryAcademicStatusAsync(factory, studentId);
        status.Should().Be("Suspended", "ESC-70: academic_status must transition to Suspended");

        // StudentStatusUpdated is staged in the outbox atomically with the status UPDATE (Gotcha 28).
        // Assert the OutboxMessage row via raw SQL (HTTP path stages but does not deliver to in-memory harness — Gotcha 30).
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentStatusUpdated%' AND \"Body\" LIKE '%{studentId}%'";
        var outboxCount = (long)(await cmd.ExecuteScalarAsync())!;
        outboxCount.Should().BeGreaterThan(0,
            "ESC-70: StudentStatusUpdated must be staged in the outbox atomically with the status change");
    }

    /// <summary>ESC-76 — Secretaria role also accepted by SecretariaOrDireccion policy → 200.</summary>
    [Fact]
    public async Task Suspend_WithSecretariaRole_Returns200()
    {
        var studentId = await EnrollStudentAsync("P4S0000002");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsync($"/api/academic/students/{studentId}/suspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>ESC-74 — No token → 401.</summary>
    [Fact]
    public async Task Suspend_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/suspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>ESC-75 — Docente role → 403.</summary>
    [Fact]
    public async Task Suspend_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/suspend", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
