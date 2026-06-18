using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Obligations.ConfirmPayment;
using Payments.Application.Obligations.RegisterObligation;
using Payments.Infrastructure.Persistence;
using Payments.Infrastructure.Persistence.ReadModels;
using Payments.Tests.Helpers;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/payments/obligations/{id}/confirm
/// (REQ-PM1-04..REQ-PM1-08, ESC-PM-10..ESC-PM-20).
///
/// NOTE on PaymentConfirmed atomicity (Gotcha 28):
/// The handler calls IPublishEndpoint.Publish before returning (inside UoW tx).
/// With EF Core outbox, the OutboxMessage row is inserted atomically with the obligation UPDATE.
/// We verify this via raw SQL on OutboxMessage — same pattern as Academic EnrollStudentIntegrationTests.
/// (The delivery service is async, but the row persists until the relay picks it up — in test
///  scenarios with Testcontainers postgres the row is present immediately post-commit.)
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class ConfirmPaymentIntegrationTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string ValidStudentId() => NUlid.Ulid.NewUlid().ToString();

    /// <summary>
    /// Phase 2 update: pre-seeds a student_replicas row so RegisterObligation passes the
    /// existence guard (ADR-056). Without this, POST obligations returns 400 student.not_found.
    /// </summary>
    private async Task<string> RegisterObligationAsync(string? studentId = null)
    {
        var sid = studentId ?? ValidStudentId();

        // Pre-seed student replica (Phase 2 guard — ADR-056). Direct DB insert, no publish-and-wait.
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
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
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new
        {
            studentId = sid,
            concept   = "Mensualidad Junio",
            amount    = 150.00m,
            dueDate   = DateTime.UtcNow.AddDays(15)
        };
        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RegisterObligationResponse>();
        return result!.ObligationId;
    }

    // ── ESC-PM-10: Happy path ─────────────────────────────────────────────────

    [Fact]
    public async Task POST_ConfirmPayment_HappyPath_Returns200()
    {
        var oblId = await RegisterObligationAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body     = new { method = "Transfer", reference = "TRX-001" };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_ConfirmPayment_HappyPath_ReturnsConfirmedStatus()
    {
        var oblId = await RegisterObligationAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body   = new { method = "Cash", reference = "EFT-002" };
        var result = await (await _client.PostAsJsonAsync(
            $"/api/payments/obligations/{oblId}/confirm", body)).Content
            .ReadFromJsonAsync<ConfirmPaymentResponse>();

        result!.Status.Should().Be("Confirmed");
        result.ObligationId.Should().Be(oblId);
        result.PaymentId.Length.Should().Be(26);
    }

    // ── ESC-PM-11: PaymentConfirmed outbox row inserted atomically (Gotcha 28) ─

    [Fact]
    public async Task POST_ConfirmPayment_HappyPath_InsertsPaymentConfirmedOutboxRow()
    {
        var sid   = ValidStudentId();
        var oblId = await RegisterObligationAsync(sid);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { method = "Card", reference = "CARD-003" };
        var resp = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify PaymentConfirmed OutboxMessage row was inserted atomically with the obligation UPDATE.
        // Raw SQL pattern mirrors Academic's EnrollStudentIntegrationTests (proven working with Testcontainers).
        // (Gotcha 28 guarantees the INSERT is inside the same transaction as the obligation UPDATE.)
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(1) FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%PaymentConfirmed%'
            AND "Body" LIKE '%{oblId}%'
            """;
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().BeGreaterThan(0, "PaymentConfirmed OutboxMessage row must be inserted atomically");
    }

    [Fact]
    public async Task POST_ConfirmPayment_HappyPath_OutboxBodyContainsAllRequiredFields()
    {
        var sid   = ValidStudentId();
        var oblId = await RegisterObligationAsync(sid);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body   = new { method = "Transfer", reference = "TRX-FIELDS-004" };
        var result = await (await _client.PostAsJsonAsync(
            $"/api/payments/obligations/{oblId}/confirm", body)).Content
            .ReadFromJsonAsync<ConfirmPaymentResponse>();

        // Verify the OutboxMessage body contains all 5 required fields (REQ-PM1-08, ADR-044)
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT "Body" FROM "OutboxMessage"
            WHERE "MessageType" LIKE '%PaymentConfirmed%'
            AND "Body" LIKE '%{oblId}%'
            ORDER BY "SentTime" DESC LIMIT 1
            """;
        var outboxBody = (string?)(await cmd.ExecuteScalarAsync());

        outboxBody.Should().NotBeNull("OutboxMessage row must exist after confirm");
        outboxBody.Should().Contain(result!.PaymentId, "PaymentId must be in the event body");
        outboxBody.Should().Contain(oblId,             "ObligationId must be in the event body");
        outboxBody.Should().Contain(sid,               "StudentId must be in the event body");
        outboxBody.Should().Contain("Transfer",        "Method must be in the event body");
        outboxBody.Should().Contain("150",             "Amount must be in the event body");
        // NOTE: Reference is NOT in event body (ADR-044) — do NOT assert on "TRX-FIELDS-004"
    }

    // ── ESC-PM-13: Idempotency — 409 on double confirm, NO second publish ─────

    [Fact]
    public async Task POST_ConfirmPayment_AlreadyConfirmed_Returns409()
    {
        var oblId = await RegisterObligationAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { method = "Cash", reference = "IDEM-005" };
        await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── ESC-PM-14: Not found ─────────────────────────────────────────────────

    [Fact]
    public async Task POST_ConfirmPayment_UnknownId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var fakeId = NUlid.Ulid.NewUlid().ToString();
        var body   = new { method = "Cash", reference = "NOTFOUND-006" };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{fakeId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── ESC-PM-16: Invalid method ──────────────────────────────────────────────

    [Fact]
    public async Task POST_ConfirmPayment_InvalidMethod_Returns400()
    {
        var oblId = await RegisterObligationAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body     = new { method = "Bitcoin", reference = "BTC-007" };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ESC-PM-18/19: Auth guards ─────────────────────────────────────────────

    [Fact]
    public async Task POST_ConfirmPayment_NoToken_Returns401()
    {
        var oblId = await RegisterObligationAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var body     = new { method = "Cash", reference = "NOAUTH-008" };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_ConfirmPayment_WrongRole_Returns403()
    {
        var oblId = await RegisterObligationAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var body     = new { method = "Cash", reference = "WRONGROLE-009" };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
