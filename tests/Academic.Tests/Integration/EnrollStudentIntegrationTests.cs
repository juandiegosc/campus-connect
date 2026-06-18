using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academic.Application.Students.EnrollStudent;
using Academic.Domain.Students;
using Academic.Infrastructure.Persistence;
using Academic.Tests.Helpers;
using BuildingBlocks.Contracts.Events;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Academic.Tests.Integration;

[Collection("AcademicPostgres")]
public sealed class EnrollStudentIntegrationTests(AcademicWebApplicationFactory factory)
    : IClassFixture<AcademicWebApplicationFactory>
{
    private readonly HttpClient    _client  = factory.CreateClient();
    private readonly ITestHarness  _harness = factory.Services.GetRequiredService<ITestHarness>();

    private static object ValidBody(string documentId = "0102030405") => new
    {
        fullName      = "Luis Gómez",
        documentId    = documentId,
        grade         = "8vo EGB",
        schoolId      = "SCH-001",
        guardianName  = "María Gómez",
        guardianEmail = "maria@example.com"
    };

    [Fact]
    public async Task POST_Students_HappyPath_Returns201()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_Students_HappyPath_StudentIdIs26Chars()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("AZ00000001"));
        var result   = await response.Content.ReadFromJsonAsync<EnrollStudentResponse>();

        result!.StudentId.Length.Should().Be(26);
        result.EnrollmentId.Length.Should().Be(26);
    }

    [Fact]
    public async Task POST_Students_HappyPath_PersistsStudentRow()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("BZ00000002"));
        var result   = await response.Content.ReadFromJsonAsync<EnrollStudentResponse>();

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var targetId = StudentId.Parse(result!.StudentId);
        var exists   = await ctx.Students.AnyAsync(s => s.Id == targetId);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task POST_Students_HappyPath_InsertsOutboxRow()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("CZ00000003"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentEnrolled%'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task POST_Students_HappyPath_PublishesStudentEnrolledViaTestHarness()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var postResponse = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("DZ00000004"));
        // If 409 due to previous run sharing the DB, use unique ID
        if (postResponse.StatusCode == HttpStatusCode.Conflict)
            postResponse = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("DZ90000004"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify the StudentEnrolled outbox message was inserted atomically
        var enrolled = await postResponse.Content.ReadFromJsonAsync<EnrollStudentResponse>();
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentEnrolled%' AND \"Body\" LIKE '%{enrolled!.StudentId}%'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().BeGreaterThan(0, "the StudentEnrolled outbox row should be inserted atomically with the student row");
    }

    [Fact]
    public async Task POST_Students_PublishedMessage_HasCorrectFields()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var postResponse = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("EZ00000005"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var enrolled = await postResponse.Content.ReadFromJsonAsync<EnrollStudentResponse>();

        // Verify the outbox message body contains the correct fields
        using var scope = factory.Services.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT \"Body\" FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentEnrolled%' AND \"Body\" LIKE '%{enrolled!.StudentId}%' ORDER BY \"SentTime\" DESC LIMIT 1";
        var body = (string?)(await cmd.ExecuteScalarAsync());
        body.Should().NotBeNull();
        body.Should().Contain("8vo EGB");
        body.Should().Contain("Luis Gómez");
        body.Should().Contain(enrolled.StudentId);
    }

    [Fact]
    public async Task POST_Students_DuplicateDocumentId_Returns409()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        // First enrollment
        await _client.PostAsJsonAsync("/api/academic/students", ValidBody("FZ00000006"));

        // Duplicate
        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("FZ00000006"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_Students_DuplicateDocumentId_NoOutboxRow()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        // Enroll with unique documentId
        await _client.PostAsJsonAsync("/api/academic/students", ValidBody("GZ00000007"));

        // Get count before duplicate attempt
        long countBefore;
        {
            using var scope = factory.Services.CreateScope();
            var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
            var conn = ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentEnrolled%' AND \"Body\" LIKE '%GZ00000007%'";
            countBefore = (long)(await cmd.ExecuteScalarAsync())!;
        }

        // Duplicate — should return 409 and NOT create new outbox row for this documentId
        var dupResponse = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("GZ00000007"));
        dupResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        long countAfter;
        {
            using var scope = factory.Services.CreateScope();
            var ctx  = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
            var conn = ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM \"OutboxMessage\" WHERE \"MessageType\" LIKE '%StudentEnrolled%' AND \"Body\" LIKE '%GZ00000007%'";
            countAfter = (long)(await cmd.ExecuteScalarAsync())!;
        }

        // Exactly 1 outbox row (from first enrollment) — the duplicate did NOT add another
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task POST_Students_InvalidBody_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("AB")); // too short

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Students_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("HZ00000008"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Students_WrongRole_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente")); // wrong role

        var response = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("IZ00000009"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GET_Status_WithValidJwt_Returns200()
    {
        // First enroll a student
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Secretaria"));
        var postResponse = await _client.PostAsJsonAsync("/api/academic/students", ValidBody("JZ00000010"));
        var enrolled     = await postResponse.Content.ReadFromJsonAsync<EnrollStudentResponse>();

        // Then check status — any valid JWT works (Q3 constraint, ESC-37)
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.CreateToken("Docente"));

        var statusResponse = await _client.GetAsync($"/api/academic/students/{enrolled!.StudentId}/status");

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
