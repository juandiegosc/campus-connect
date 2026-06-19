using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Attendance.Application.Incidents.ReportIncident;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.ReadModels;
using Attendance.Tests.Helpers;
using Xunit;

namespace Attendance.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/attendance/incidents (REQ-AT1-20..REQ-AT1-24, ESC-AT-11..ESC-AT-20).
///
/// KEY INVARIANT (REQ-AT1-13, ADR-070): Description NEVER appears in IncidentReported event body (frozen contract).
/// Description IS stored in DB incidents table.
/// Outbox assertions use RAW SQL (Gotcha 30 / ADR-R6).
/// </summary>
[Collection("AttendancePostgres")]
public sealed class IncidentIntegrationTests(AttendanceWebApplicationFactory factory)
    : IClassFixture<AttendanceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string ValidStudentId() => NUlid.Ulid.NewUlid().ToString();

    private async Task<string> SeedStudentAndGetId(string? studentId = null)
    {
        var sid = studentId ?? ValidStudentId();
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        if (!await ctx.StudentReplicas.AnyAsync(s => s.StudentId == sid))
        {
            ctx.StudentReplicas.Add(new StudentReplica
            {
                StudentId     = sid,
                FullName      = "Test Student",
                Grade         = "5A",
                SchoolId      = "SCH-001",
                LastUpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }
        return sid;
    }

    // ── ESC-AT-11: Happy path — 201 + valid incidentId ───────────────────────

    [Fact]
    public async Task POST_Incidents_HappyPath_Returns201()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body = new { studentId = sid, type = "Misconduct", severity = "Low", description = "Minor disruption" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_Incidents_HappyPath_ReturnsIncidentIdAnd26Chars()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body     = new { studentId = sid, type = "Absence", severity = "Medium", description = "Repeated absence" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);
        var result   = await response.Content.ReadFromJsonAsync<ReportIncidentResponse>();

        result!.IncidentId.Length.Should().Be(26, "IncidentId must be a ULID (26 chars)");
        result.Severity.Should().Be("Medium");
    }

    // ── ESC-AT-12: IncidentReported outbox row inserted atomically ────────────

    [Fact]
    public async Task POST_Incidents_HappyPath_InsertsIncidentReportedOutboxRow()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body     = new { studentId = sid, type = "Aggression", severity = "High", description = "Physical altercation" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReportIncidentResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(1) FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%IncidentReported%'
            AND "Body" LIKE '%{result!.IncidentId}%'
            """;
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().BeGreaterThan(0,
            "IncidentReported OutboxMessage row must be inserted atomically (ADR-075)");
    }

    // ── ESC-AT-13: IncidentReported outbox body — 4 fields, NO description ────

    /// <summary>
    /// CRITICAL INVARIANT (REQ-AT1-13, ADR-070, frozen contract):
    /// IncidentReported body must contain exactly 4 fields (IncidentId, StudentId, Type, Severity).
    /// Description must NEVER appear in the event body — one-way door.
    /// Simultaneously: description IS stored in incidents table (REQ-AT1-24).
    /// </summary>
    [Fact]
    public async Task POST_Incidents_HappyPath_OutboxBodyContainsFourFieldsNoDescription()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var uniqueDesc = $"description-sentinel-{Guid.NewGuid():N}";
        var body       = new { studentId = sid, type = "Tardiness", severity = "Low", description = uniqueDesc };
        var response   = await _client.PostAsJsonAsync("/api/attendance/incidents", body);
        var result     = await response.Content.ReadFromJsonAsync<ReportIncidentResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT "Body" FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%IncidentReported%'
            AND "Body" LIKE '%{result!.IncidentId}%'
            ORDER BY "SentTime" DESC LIMIT 1
            """;
        var outboxBody = (string?)(await cmd.ExecuteScalarAsync());

        outboxBody.Should().NotBeNull("OutboxMessage row must exist");
        // 4 required fields
        outboxBody.Should().Contain(result!.IncidentId, "IncidentId (4 fields contract)");
        outboxBody.Should().Contain(sid,                "StudentId (4 fields contract)");
        outboxBody.Should().Contain("Tardiness",        "Type (4 fields contract)");
        outboxBody.Should().Contain("Low",              "Severity (4 fields contract)");
        // ONE-WAY DOOR: Description must NEVER be in the event body (REQ-AT1-13)
        outboxBody.Should().NotContain(uniqueDesc,      "Description must NOT appear in IncidentReported event (frozen contract, one-way door)");
        outboxBody.Should().NotContain("description",   "Description field must NOT appear in IncidentReported event");
    }

    // ── ESC-AT-14: Description IS stored in DB (REQ-AT1-24) ──────────────────

    [Fact]
    public async Task POST_Incidents_HappyPath_DescriptionPersistedInDb()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var uniqueDesc = $"stored-desc-{Guid.NewGuid():N}";
        var body       = new { studentId = sid, type = "Cheating", severity = "Medium", description = uniqueDesc };
        var response   = await _client.PostAsJsonAsync("/api/attendance/incidents", body);
        var result     = await response.Content.ReadFromJsonAsync<ReportIncidentResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT description FROM incidents WHERE incident_id = '{result!.IncidentId}'
            """;
        var dbDesc = (string?)(await cmd.ExecuteScalarAsync());

        dbDesc.Should().Be(uniqueDesc, "Description must be stored in incidents table (REQ-AT1-24)");
    }

    // ── ESC-AT-17: Invalid severity → 400 ────────────────────────────────────

    [Fact]
    public async Task POST_Incidents_InvalidSeverity_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body = new { studentId = ValidStudentId(), type = "Misconduct", severity = "Critical", description = "X" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ESC-AT-18: Unknown student → 400 student.not_found ───────────────────

    [Fact]
    public async Task POST_Incidents_UnknownStudentId_Returns400_StudentNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var unknownSid = ValidStudentId();
        var body = new { studentId = unknownSid, type = "Misconduct", severity = "Low", description = "X" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ESC-AT-18: unknown student must yield HTTP 400 (ADR-056 Error.Validation→400)");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("student.not_found");
    }

    // ── ESC-AT-19/20: Auth guards ─────────────────────────────────────────────

    [Fact]
    public async Task POST_Incidents_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var body = new { studentId = ValidStudentId(), type = "Misconduct", severity = "Low", description = "X" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Incidents_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.SecretariaToken());

        var body = new { studentId = ValidStudentId(), type = "Misconduct", severity = "Low", description = "X" };
        var response = await _client.PostAsJsonAsync("/api/attendance/incidents", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
