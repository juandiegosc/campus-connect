using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Obligations.RegisterObligation;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Persistence.ReadModels;
using Payments.Tests.Helpers;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/payments/obligations (REQ-PM1-01..REQ-PM1-03, ESC-PM-01..ESC-PM-09).
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class RegisterObligationIntegrationTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Valid ULID-length student id (26 chars)
    private static string ValidStudentId() => NUlid.Ulid.NewUlid().ToString();

    private static object ValidBody(string? studentId = null) => new
    {
        studentId = studentId ?? ValidStudentId(),
        concept   = "Matrícula 2026",
        amount    = 250.00m,
        dueDate   = DateTime.UtcNow.AddDays(30)
    };

    /// <summary>
    /// Phase 2: pre-seed a student_replicas row so the existence guard (ADR-056) passes.
    /// Direct DB insert — deterministic, no eventual-consistency race.
    /// </summary>
    private async Task<string> SeedStudentAndGetId()
    {
        var sid = ValidStudentId();
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        ctx.StudentReplicas.Add(new StudentReplica
        {
            StudentId     = sid,
            FullName      = "Test Student",
            Grade         = "5A",
            SchoolId      = "SCH-001",
            LastUpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return sid;
    }

    // ── ESC-PM-01: Happy path ─────────────────────────────────────────────────

    [Fact]
    public async Task POST_Obligations_HappyPath_Returns201()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody(sid));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_Obligations_HappyPath_ReturnsObligationIdAnd26Chars()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody(sid));
        var result   = await response.Content.ReadFromJsonAsync<RegisterObligationResponse>();

        result!.ObligationId.Length.Should().Be(26);
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task POST_Obligations_HappyPath_PersistsRow()
    {
        var sid = await SeedStudentAndGetId();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody(sid));
        var result   = await response.Content.ReadFromJsonAsync<RegisterObligationResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var exists = await ctx.Obligations.FindAsync(
            Payments.Domain.Obligations.ObligationId.Parse(result!.ObligationId));
        exists.Should().NotBeNull();
    }

    // ── ESC-PM-04: Validation errors ─────────────────────────────────────────

    [Fact]
    public async Task POST_Obligations_EmptyConcept_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { studentId = ValidStudentId(), concept = "", amount = 100m, dueDate = DateTime.UtcNow.AddDays(1) };
        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Obligations_ZeroAmount_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { studentId = ValidStudentId(), concept = "X", amount = 0m, dueDate = DateTime.UtcNow.AddDays(1) };
        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Obligations_InvalidStudentIdLength_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { studentId = "SHORT", concept = "X", amount = 100m, dueDate = DateTime.UtcNow.AddDays(1) };
        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ESC-PM-07/08: Auth guards ─────────────────────────────────────────────

    [Fact]
    public async Task POST_Obligations_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Obligations_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Phase 2: ESC-PM-37 — Unknown StudentId → 400 student.not_found ────────

    /// <summary>
    /// ESC-PM-37, REQ-PM2-04: No student_replicas row → POST obligations → 400 with student.not_found.
    /// Pre-condition: do NOT seed any student_replicas row for the studentId used here.
    /// </summary>
    [Fact]
    public async Task POST_UnknownStudentId_Returns400_StudentNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        // Use a valid 26-char ULID that was never enrolled — no replica row will exist.
        var unknownStudentId = ValidStudentId();
        var body = ValidBody(unknownStudentId);

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ESC-PM-37: unknown student must yield HTTP 400 (ADR-056 Error.Validation→400)");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("student.not_found",
            "response body must include the error code (MapError includes [Code] in detail field)");

        // Verify no obligation row was created
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var obligations = await ctx.Obligations
            .Where(o => o.StudentId == unknownStudentId)
            .ToListAsync();
        obligations.Should().BeEmpty("no obligation must be created for unknown student");
    }

    // ── Phase 2: ESC-PM-38 — Known StudentId → 201 (pre-seeded replica) ──────

    /// <summary>
    /// ESC-PM-38, REQ-PM2-04: Pre-seed student_replicas row → POST obligations → 201 Pending.
    /// Uses direct DB insert — avoids publish-and-wait eventual-consistency window (ADR R2).
    /// </summary>
    [Fact]
    public async Task POST_KnownStudentId_Returns201()
    {
        var studentId = ValidStudentId();

        // Direct DB insert — deterministic, no timing race (ADR R2).
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            ctx.StudentReplicas.Add(new StudentReplica
            {
                StudentId     = studentId,
                FullName      = "Ana Torres",
                Grade         = "5A",
                SchoolId      = "SCH-001",
                LastUpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody(studentId));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "ESC-PM-38: known student must succeed with 201");

        var result = await response.Content.ReadFromJsonAsync<RegisterObligationResponse>();
        result!.Status.Should().Be("Pending");
    }
}
