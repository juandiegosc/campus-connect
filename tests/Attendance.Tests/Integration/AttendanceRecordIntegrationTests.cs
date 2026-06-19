using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Attendance.Application.Attendance.RecordAttendance;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.ReadModels;
using Attendance.Tests.Helpers;
using Xunit;

namespace Attendance.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/attendance/records (REQ-AT1-17..REQ-AT1-22, ESC-AT-01..ESC-AT-10).
///
/// Outbox assertions use RAW SQL on OutboxMessage — harness.Published is NOT used because
/// UseBusOutbox deletes/delivers rows asynchronously (Gotcha 30 / ADR-R6).
/// </summary>
[Collection("AttendancePostgres")]
public sealed class AttendanceRecordIntegrationTests(AttendanceWebApplicationFactory factory)
    : IClassFixture<AttendanceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string ValidStudentId() => NUlid.Ulid.NewUlid().ToString();

    /// <summary>
    /// Seed a student_replicas row via direct EF insert.
    /// Bypasses consumer — deterministic, no eventual-consistency window (ADR R2).
    /// </summary>
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

    // ── ESC-AT-01: Happy path — 201 + valid recordId ─────────────────────────

    [Fact]
    public async Task POST_Records_HappyPath_Returns201()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body = new { studentId = sid, date = "2026-06-17", status = "Present" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_Records_HappyPath_ReturnsRecordIdAnd26Chars()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body     = new { studentId = sid, date = "2026-06-17", status = "Absent" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);
        var result   = await response.Content.ReadFromJsonAsync<RecordAttendanceResponse>();

        result!.RecordId.Length.Should().Be(26, "RecordId must be a ULID (26 chars)");
        result.Status.Should().Be("Absent",     "status must match the parsed enum value");
    }

    // ── ESC-AT-02: AttendanceRecorded outbox row inserted atomically ──────────

    [Fact]
    public async Task POST_Records_HappyPath_InsertsAttendanceRecordedOutboxRow()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body     = new { studentId = sid, date = "2026-06-01", status = "Late" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RecordAttendanceResponse>();

        // Raw SQL: verify OutboxMessage row (Gotcha 30 / ADR-R6).
        // Row is inserted atomically with EF SaveChanges inside UoW tx.
        // (Delivery service is async — row present immediately post-commit.)
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(1) FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%AttendanceRecorded%'
            AND "Body" LIKE '%{result!.RecordId}%'
            """;
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().BeGreaterThan(0,
            "AttendanceRecorded OutboxMessage row must be inserted atomically (ADR-075)");
    }

    [Fact]
    public async Task POST_Records_HappyPath_OutboxBodyContainsFourRequiredFields()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body     = new { studentId = sid, date = "2026-06-10", status = "Present" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);
        var result   = await response.Content.ReadFromJsonAsync<RecordAttendanceResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT "Body" FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%AttendanceRecorded%'
            AND "Body" LIKE '%{result!.RecordId}%'
            ORDER BY "SentTime" DESC LIMIT 1
            """;
        var outboxBody = (string?)(await cmd.ExecuteScalarAsync());

        outboxBody.Should().NotBeNull("OutboxMessage row must exist");
        outboxBody.Should().Contain(result!.RecordId, "RecordId (4 fields contract)");
        outboxBody.Should().Contain(sid,              "StudentId (4 fields contract)");
        outboxBody.Should().Contain("2026-06-10",     "Date ISO (4 fields contract)");
        outboxBody.Should().Contain("Present",        "Status (4 fields contract)");
        // REQ-AT1-12 / frozen contract: no Description field allowed
        outboxBody.Should().NotContain("description", "AttendanceRecorded must NOT have Description (frozen contract)");
    }

    // ── ESC-AT-05: Invalid status → 400 ──────────────────────────────────────

    [Fact]
    public async Task POST_Records_InvalidStatus_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var body = new { studentId = ValidStudentId(), date = "2026-06-17", status = "Tardy" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ESC-AT-06: Unknown student → 400 student.not_found ───────────────────

    [Fact]
    public async Task POST_Records_UnknownStudentId_Returns400_StudentNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var unknownSid = ValidStudentId();
        var body = new { studentId = unknownSid, date = "2026-06-17", status = "Present" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ESC-AT-06: unknown student must yield HTTP 400 (ADR-056 Error.Validation→400)");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("student.not_found",
            "response body must include the error code");
    }

    // ── ESC-AT-08/09: Auth guards ─────────────────────────────────────────────

    [Fact]
    public async Task POST_Records_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var body = new { studentId = ValidStudentId(), date = "2026-06-17", status = "Present" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Records_DireccionToken_Returns403()
    {
        // POST /records requires "Docente" policy — Direccion alone is not enough (REQ-AT1-34)
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DireccionToken());

        var body = new { studentId = ValidStudentId(), date = "2026-06-17", status = "Present" };
        var response = await _client.PostAsJsonAsync("/api/attendance/records", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
