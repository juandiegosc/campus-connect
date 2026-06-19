using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Attendance.Application.Students.Shared;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.ReadModels;
using Attendance.Tests.Helpers;
using Xunit;

namespace Attendance.Tests.Integration;

/// <summary>
/// Integration tests for GET /api/attendance/students/{id}/history (REQ-AT1-29, ESC-AT-28..ESC-AT-33).
/// Policy: "DocenteOrDireccion" — both roles can access; Secretaria cannot.
/// Response shape: StudentHistoryDto { Attendance: [...], Incidents: [...] }.
/// </summary>
[Collection("AttendancePostgres")]
public sealed class GetStudentHistoryIntegrationTests(AttendanceWebApplicationFactory factory)
    : IClassFixture<AttendanceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewStudentId() => NUlid.Ulid.NewUlid().ToString();

    private async Task<string> SeedStudentAndGetId(string? studentId = null)
    {
        var sid = studentId ?? NewStudentId();
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        ctx.StudentReplicas.Add(new StudentReplica
        {
            StudentId     = sid,
            FullName      = "History Student",
            Grade         = "5A",
            SchoolId      = "SCH-001",
            LastUpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return sid;
    }

    // ── ESC-AT-28: No token → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task GET_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync($"/api/attendance/students/{NewStudentId()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── ESC-AT-29: Secretaria → 403 ──────────────────────────────────────────

    [Fact]
    public async Task GET_SecretariaToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.SecretariaToken());

        var response = await _client.GetAsync($"/api/attendance/students/{NewStudentId()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── ESC-AT-30: Docente → 200 with history ────────────────────────────────

    [Fact]
    public async Task GET_DocenteToken_Returns200WithHistory()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var response = await _client.GetAsync($"/api/attendance/students/{sid}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<StudentHistoryDto>();
        history.Should().NotBeNull();
        history!.Attendance.Should().NotBeNull();
        history.Incidents.Should().NotBeNull();
    }

    // ── ESC-AT-31: Direccion → 200 with history ──────────────────────────────

    [Fact]
    public async Task GET_DireccionToken_Returns200WithHistory()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DireccionToken());

        var response = await _client.GetAsync($"/api/attendance/students/{sid}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<StudentHistoryDto>();
        history.Should().NotBeNull();
    }

    // ── ESC-AT-32: Unknown studentId → 404 ───────────────────────────────────

    [Fact]
    public async Task GET_UnknownStudentId_Returns404()
    {
        var unknownSid = NewStudentId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var response = await _client.GetAsync($"/api/attendance/students/{unknownSid}/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── ESC-AT-33: Seeded records appear in history ───────────────────────────

    [Fact]
    public async Task GET_StudentWithRecords_HistoryContainsAttendanceAndIncidents()
    {
        var sid = await SeedStudentAndGetId();

        // POST a record using Docente token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        await _client.PostAsJsonAsync("/api/attendance/records",
            new { studentId = sid, date = "2026-06-17", status = "Present" });

        await _client.PostAsJsonAsync("/api/attendance/incidents",
            new { studentId = sid, type = "Tardiness", severity = "Low", description = "Came late" });

        var response = await _client.GetAsync($"/api/attendance/students/{sid}/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<StudentHistoryDto>();
        history!.Attendance.Should().HaveCountGreaterThan(0,
            "ESC-AT-33: at least 1 attendance record must appear");
        history.Incidents.Should().HaveCountGreaterThan(0,
            "ESC-AT-33: at least 1 incident must appear");

        // Incidents history must NOT contain Description field (REQ-AT1-26)
        var firstIncident = history.Incidents.First();
        var incidentType = firstIncident.GetType();
        incidentType.GetProperty("Description").Should().BeNull(
            "IncidentSummaryDto must not expose Description (REQ-AT1-26)");
    }
}
