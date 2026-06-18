using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Obligations.RegisterObligation;
using Payments.Infrastructure.Persistence;
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

    // ── ESC-PM-01: Happy path ─────────────────────────────────────────────────

    [Fact]
    public async Task POST_Obligations_HappyPath_Returns201()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_Obligations_HappyPath_ReturnsObligationIdAnd26Chars()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.PostAsJsonAsync("/api/payments/obligations", ValidBody());
        var result   = await response.Content.ReadFromJsonAsync<RegisterObligationResponse>();

        result!.ObligationId.Length.Should().Be(26);
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task POST_Obligations_HappyPath_PersistsRow()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var sid      = ValidStudentId();
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
}
