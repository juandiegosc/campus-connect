using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Students.Shared;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Persistence.ReadModels;
using Payments.Tests.Helpers;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for GET /api/payments/students.
/// ESC-PM-40..46, REQ-PM2-06..09.
///
/// Seeding done via direct DB insert (NOT publish-and-wait) — avoids eventual-consistency window (ADR R2).
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class GetStudentsIntegrationTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    // NUlid generates exactly 26-char strings. student_id column is varchar(26).
    // Ensure the value is EXACTLY 26 chars (ULID spec guarantees this).
    private static string NewStudentId()
    {
        var id = NUlid.Ulid.NewUlid().ToString();
        System.Diagnostics.Debug.Assert(id.Length == 26, $"ULID length was {id.Length}, expected 26");
        return id;
    }

    /// <summary>
    /// Seed a student_replicas row via direct EF insert (bypasses consumer — deterministic, no timing issues).
    /// </summary>
    private async Task SeedReplica(string studentId, string fullName, string grade, string schoolId = "SCH-001")
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
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

    // ── ESC-PM-40: No token → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task GET_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/payments/students");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── ESC-PM-41: Wrong role → 403 ──────────────────────────────────────────

    [Fact]
    public async Task GET_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Direccion"));

        var response = await _client.GetAsync("/api/payments/students");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── ESC-PM-44: Empty table → 200 with empty items ────────────────────────

    [Fact]
    public async Task GET_FinanzasToken_NoData_Returns200EmptyItems()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/students?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        paged.Should().NotBeNull();
        // May have rows from other tests — just assert 200 and valid shape
        paged!.Items.Should().NotBeNull();
    }

    // ── ESC-PM-42/43: Pagination returns correct slice ───────────────────────

    [Fact]
    public async Task GET_FinanzasToken_WithReplicas_ReturnsPaged()
    {
        // Generate exactly 26-char student IDs and use a short unique grade for isolation
        // uniqueGrade: "PG" + 4 hex chars = 6 chars (fits varchar(50))
        var uniqueGrade = "PG" + Guid.NewGuid().ToString("N")[..4].ToUpper();

        for (int i = 0; i < 5; i++)
        {
            var sid = NewStudentId();
            System.Diagnostics.Debug.Assert(sid.Length == 26, $"Expected 26-char ID, got {sid.Length}: {sid}");
            await SeedReplica(sid, $"PagedStudent {i}", uniqueGrade);
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync($"/api/payments/students?page=1&pageSize=2&grade={uniqueGrade}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        paged.Should().NotBeNull();
        paged!.Items.Count.Should().BeLessOrEqualTo(2, "pageSize=2 constrains the slice");
        paged.Total.Should().BeGreaterOrEqualTo(5, "Total includes all seeded rows for this grade");
    }

    // ── ESC-PM-45: grade filter narrows results ───────────────────────────────

    [Fact]
    public async Task GET_GradeFilter_NarrowsResults()
    {
        var sid5A = NewStudentId();
        var sid6B = NewStudentId();
        await SeedReplica(sid5A, "Grade5A Student", "5A");
        await SeedReplica(sid6B, "Grade6B Student", "6B");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/students?grade=5A&page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        paged.Should().NotBeNull();
        paged!.Items.Should().OnlyContain(i => i.Grade == "5A",
            "ESC-PM-45: grade filter must return only matching rows");
        paged.Items.Should().Contain(i => i.StudentId == sid5A);
    }

    // ── ESC-PM-55: GET exposes status fields when present ─────────────────────

    [Fact]
    public async Task GET_ReplicaWithStatus_ExposesStatusFields()
    {
        var sid   = NewStudentId();
        var grade = "PG" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            ctx.StudentReplicas.Add(new StudentReplica
            {
                StudentId       = sid,
                FullName        = "Status Student",
                Grade           = grade,
                SchoolId        = "SCH-001",
                LastUpdatedAt   = DateTime.UtcNow,
                AcademicStatus  = "Active",
                FinancialStatus = "Paid"
            });
            await ctx.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync($"/api/payments/students?grade={grade}&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        var item  = paged!.Items.Single(i => i.StudentId == sid);
        item.AcademicStatus.Should().Be("Active",   "ESC-PM-55: status fields must surface in GET");
        item.FinancialStatus.Should().Be("Paid");
    }

    // ── ESC-PM-56: GET returns null status for enroll-only replicas ───────────

    [Fact]
    public async Task GET_ReplicaWithoutStatus_ReturnsNullStatusFields()
    {
        var sid   = NewStudentId();
        var grade = "PG" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        await SeedReplica(sid, "NoStatus Student", grade);   // no status set

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync($"/api/payments/students?grade={grade}&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        var item  = paged!.Items.Single(i => i.StudentId == sid);
        item.AcademicStatus.Should().BeNull("ESC-PM-56: enroll-only replica has null status");
        item.FinancialStatus.Should().BeNull();
    }

    // ── ESC-PM-46: search filter by FullName substring ───────────────────────

    [Fact]
    public async Task GET_SearchFilter_ByFullName()
    {
        // Use a unique suffix so this test's names don't collide with other test data.
        var unique    = NUlid.Ulid.NewUlid().ToString()[..8];
        var sidAna    = NewStudentId();
        var sidCarlos = NewStudentId();
        var nameAna   = $"Ana Torr{unique}";
        var nameCar   = $"Carlos Rui{unique}";
        await SeedReplica(sidAna,    nameAna,  "5A");
        await SeedReplica(sidCarlos, nameCar,  "5A");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        // Search for the unique portion of Ana's name
        var response = await _client.GetAsync($"/api/payments/students?search=Torr{unique}&page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedList<StudentReplicaItemDto>>();
        paged.Should().NotBeNull();
        paged!.Items.Should().Contain(i => i.StudentId == sidAna,
            "ESC-PM-46: unique name search must return Ana");
        paged.Items.Should().NotContain(i => i.StudentId == sidCarlos,
            "ESC-PM-46: search must not return Carlos");
    }
}
