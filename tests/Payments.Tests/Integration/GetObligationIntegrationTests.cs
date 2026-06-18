using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Payments.Application.Obligations.ConfirmPayment;
using Payments.Application.Obligations.GetObligationById;
using Payments.Application.Obligations.GetObligations;
using Payments.Application.Obligations.RegisterObligation;
using Payments.Tests.Helpers;
using Xunit;

namespace Payments.Tests.Integration;

/// <summary>
/// Integration tests for GET /api/payments/obligations and GET /api/payments/obligations/{id}
/// (REQ-PM1-09..REQ-PM1-10, ESC-PM-21..ESC-PM-30).
/// </summary>
[Collection("PaymentsPostgres")]
public sealed class GetObligationIntegrationTests(PaymentsWebApplicationFactory factory)
    : IClassFixture<PaymentsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string ValidStudentId() => NUlid.Ulid.NewUlid().ToString();

    private async Task<RegisterObligationResponse> RegisterAsync(string? sid = null, decimal amount = 100m, string concept = "Test Concept")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new
        {
            studentId = sid ?? ValidStudentId(),
            concept   = concept,
            amount    = amount,
            dueDate   = DateTime.UtcNow.AddDays(30)
        };
        var response = await _client.PostAsJsonAsync("/api/payments/obligations", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegisterObligationResponse>())!;
    }

    private async Task<ConfirmPaymentResponse> ConfirmAsync(string oblId, string method = "Cash", string reference = "REF-001")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var body = new { method, reference };
        var response = await _client.PostAsJsonAsync($"/api/payments/obligations/{oblId}/confirm", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConfirmPaymentResponse>())!;
    }

    // ── GET /api/payments/obligations ─────────────────────────────────────────

    [Fact]
    public async Task GET_Obligations_Returns200WithList()
    {
        await RegisterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/obligations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ObligationListItemDto>>();
        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GET_Obligations_FilterByPending_ReturnsPendingOnly()
    {
        var sid1   = ValidStudentId();
        var sid2   = ValidStudentId();
        var reg1   = await RegisterAsync(sid1);
        var reg2   = await RegisterAsync(sid2);

        // Confirm the second obligation so there's a mix
        await ConfirmAsync(reg2.ObligationId, "Transfer", "TRX-LIST-001");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/obligations?status=Pending");
        var items    = await response.Content.ReadFromJsonAsync<List<ObligationListItemDto>>();

        items.Should().NotBeNull();
        items!.Should().OnlyContain(x => x.Status == "Pending");
    }

    [Fact]
    public async Task GET_Obligations_FilterByConfirmed_ReturnsConfirmedOnly()
    {
        var sid   = ValidStudentId();
        var reg   = await RegisterAsync(sid);
        await ConfirmAsync(reg.ObligationId, "Card", "CARD-LIST-002");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/obligations?status=Confirmed");
        var items    = await response.Content.ReadFromJsonAsync<List<ObligationListItemDto>>();

        items.Should().NotBeNull();
        items!.Should().OnlyContain(x => x.Status == "Confirmed");
    }

    [Fact]
    public async Task GET_Obligations_InvalidStatusFilter_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync("/api/payments/obligations?status=INVALID");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_Obligations_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/payments/obligations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Obligations_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Direccion"));

        var response = await _client.GetAsync("/api/payments/obligations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/payments/obligations/{id} ────────────────────────────────────

    [Fact]
    public async Task GET_ObligationById_PendingObligation_Returns200WithNullPayment()
    {
        var reg = await RegisterAsync(concept: "Matricula GET-001");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync($"/api/payments/obligations/{reg.ObligationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ObligationDetailDto>();
        detail!.ObligationId.Should().Be(reg.ObligationId);
        detail.Status.Should().Be("Pending");
        detail.Payment.Should().BeNull();
    }

    [Fact]
    public async Task GET_ObligationById_ConfirmedObligation_IncludesPaymentDetail()
    {
        var reg     = await RegisterAsync(concept: "Mensualidad GET-002", amount: 200m);
        var confirm = await ConfirmAsync(reg.ObligationId, "Transfer", "TRX-GET-002");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var response = await _client.GetAsync($"/api/payments/obligations/{reg.ObligationId}");
        var detail   = await response.Content.ReadFromJsonAsync<ObligationDetailDto>();

        detail!.Status.Should().Be("Confirmed");
        detail.Payment.Should().NotBeNull();
        detail.Payment!.Method.Should().Be("Transfer");
        detail.Payment.Reference.Should().Be("TRX-GET-002");
        detail.Payment.PaymentId.Should().Be(confirm.PaymentId);
        detail.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task GET_ObligationById_UnknownId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Finanzas"));

        var fakeId   = NUlid.Ulid.NewUlid().ToString();
        var response = await _client.GetAsync($"/api/payments/obligations/{fakeId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_ObligationById_NoToken_Returns401()
    {
        var reg = await RegisterAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync($"/api/payments/obligations/{reg.ObligationId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ObligationById_WrongRole_Returns403()
    {
        var reg = await RegisterAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.GetAsync($"/api/payments/obligations/{reg.ObligationId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
