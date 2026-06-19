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
/// Integration tests for GET /api/attendance/students (REQ-AT1-28, ESC-AT-25..ESC-AT-27).
/// Attendance GET students uses Docente policy (unlike Payments which uses Finanzas).
/// Seeding done via direct DB insert — avoids eventual-consistency window (ADR R2).
/// </summary>
[Collection("AttendancePostgres")]
public sealed class GetStudentsIntegrationTests(AttendanceWebApplicationFactory factory)
    : IClassFixture<AttendanceWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewStudentId() => NUlid.Ulid.NewUlid().ToString();

    private async Task SeedReplica(string studentId, string fullName, string grade, string schoolId = "SCH-001")
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        ctx.StudentReplicas.Add(new StudentReplica
        {
            StudentId     = studentId,
            FullName      = fullName,
            Grade         = grade,
            SchoolId      = schoolId,
            LastUpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    // ── ESC-AT-25: No token → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task GET_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/attendance/students");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── ESC-AT-26: Wrong role (Secretaria) → 403 ─────────────────────────────

    [Fact]
    public async Task GET_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.SecretariaToken());

        var response = await _client.GetAsync("/api/attendance/students");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── ESC-AT-27: Docente → 200 with student list ───────────────────────────

    [Fact]
    public async Task GET_DocenteToken_Returns200WithList()
    {
        var sid = NewStudentId();
        await SeedReplica(sid, "Lista Student", "5A");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.DocenteToken());

        var response = await _client.GetAsync("/api/attendance/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<StudentReplicaDto>>();
        list.Should().NotBeNull();
        list.Should().Contain(s => s.StudentId == sid,
            "seeded student must appear in GET students response");
    }
}
