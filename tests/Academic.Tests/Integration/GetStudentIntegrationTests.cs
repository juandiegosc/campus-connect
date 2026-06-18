using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Application.Students.EnrollStudent;
using Academic.Application.Students.Shared;
using Academic.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Academic.Tests.Integration;

[Collection("AcademicPostgres")]
public sealed class GetStudentIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private void UseSecretariaToken()
        => _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

    private async Task<EnrollStudentResponse> EnrollAsync(string documentId)
    {
        UseSecretariaToken();
        var body = new
        {
            fullName      = "Test Student",
            documentId    = documentId,
            grade         = "10mo EGB",
            schoolId      = "SCH-001",
            guardianName  = "Test Guardian",
            guardianEmail = "guardian@example.com"
        };
        var resp = await _client.PostAsJsonAsync("/api/academic/students", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<EnrollStudentResponse>())!;
    }

    [Fact]
    public async Task GET_Students_Returns200_WithPaginatedList()
    {
        await EnrollAsync("KZ00000011");

        UseSecretariaToken();
        var response = await _client.GetAsync("/api/academic/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GET_Students_WithGradeFilter_ReturnsFilteredResults()
    {
        await EnrollAsync("LZ00000012");

        UseSecretariaToken();
        var response = await _client.GetAsync("/api/academic/students?grade=10mo+EGB&page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GET_StudentById_ExistingId_Returns200_WithAllFields()
    {
        var enrolled = await EnrollAsync("MZ00000013");

        UseSecretariaToken();
        var response = await _client.GetAsync($"/api/academic/students/{enrolled.StudentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<StudentDetailDto>();
        dto.Should().NotBeNull();
        dto!.StudentId.Should().Be(enrolled.StudentId);
        dto.FullName.Should().Be("Test Student");
        dto.DocumentId.Should().Be("MZ00000013");
        dto.Guardian.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_StudentById_NonExistentId_Returns404()
    {
        UseSecretariaToken();
        var nonExistentId = NUlid.Ulid.NewUlid().ToString();

        var response = await _client.GetAsync($"/api/academic/students/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_StudentStatus_WithValidJwt_NoRole_Returns200()
    {
        var enrolled = await EnrollAsync("NZ00000014");

        // Use a token with a role that has NO named policy mapping (Docente)
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var response = await _client.GetAsync($"/api/academic/students/{enrolled.StudentId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<StudentStatusDto>();
        dto!.Exists.Should().BeTrue();
        dto.AcademicStatus.Should().Be("Active");
    }

    [Fact]
    public async Task GET_StudentStatus_NonExistentId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));
        var nonExistentId = NUlid.Ulid.NewUlid().ToString();

        var response = await _client.GetAsync($"/api/academic/students/{nonExistentId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_StudentEvents_AfterEnroll_Returns200()
    {
        var enrolled = await EnrollAsync("OZ00000015");

        UseSecretariaToken();
        var response = await _client.GetAsync($"/api/academic/students/{enrolled.StudentId}/events");

        // The endpoint returns 200 — events may be empty in test (outbox delivers async)
        // Actual event publish is verified via TestHarness in EnrollStudentIntegrationTests
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
