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
/// Integration tests for POST /api/academic/students/{id}/graduate (Phase 4, ADR-066/067/068).
/// Graduate is Direccion-only (ADR-067) and TERMINAL (ADR-066).
/// Shares the Postgres container via [Collection("AcademicPostgres")]; isolation via unique DocumentIds (prefix: P4G).
/// ESC-83..ESC-89.
/// </summary>
[Collection("AcademicPostgres")]
public sealed class GraduateStudentIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> EnrollStudentAsync(string documentId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var body = new
        {
            fullName      = "Graduate Test Student",
            documentId    = documentId,
            grade         = "9no EGB",
            schoolId      = "SCH-001",
            guardianName  = "Graduate Guardian",
            guardianEmail = "graduate-guardian@example.com"
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

    /// <summary>ESC-83/ESC-89 — Happy path (Direccion token, Active student) → 200, academic_status=Graduated, outbox row.</summary>
    [Fact]
    public async Task Graduate_ActiveStudent_WithDireccion_Returns200_SetsGraduated_PublishesStatus()
    {
        var studentId = await EnrollStudentAsync("P4G0000001");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Direccion"));

        var response = await _client.PostAsync($"/api/academic/students/{studentId}/graduate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await QueryAcademicStatusAsync(factory, studentId);
        status.Should().Be("Graduated", "ESC-83: academic_status must transition to Graduated");

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
            "ESC-83: StudentStatusUpdated must be staged in the outbox atomically with the graduation");
    }

    /// <summary>ESC-87 — No token → 401.</summary>
    [Fact]
    public async Task Graduate_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/graduate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>ESC-88 — Secretaria role → 403 (Direccion-only boundary, ADR-067).</summary>
    [Fact]
    public async Task Graduate_WithSecretariaRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/graduate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ESC-88b — Docente role → 403.</summary>
    [Fact]
    public async Task Graduate_WithDocenteRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var response = await _client.PostAsync(
            $"/api/academic/students/{NUlid.Ulid.NewUlid()}/graduate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
