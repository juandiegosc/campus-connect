using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Identity.Application.Abstractions;
using Identity.Application.Users.Login;
using Identity.Application.Users.GetCurrentUser;
using Identity.Domain.Users;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Identity.Tests.Integration;

/// <summary>
/// Integration tests for the full authentication HTTP flow.
/// Requires Docker daemon running — uses a real Postgres container via Testcontainers (ADR-029).
/// All scenarios: ESC-22, ESC-32, ESC-33, ESC-55–64.
/// </summary>
[Collection("Postgres")]
public sealed class AuthFlowIntegrationTests : IClassFixture<IdentityWebApplicationFactory>
{
    private readonly IdentityWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowIntegrationTests(IdentityWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ApplyMigrationsAsync().GetAwaiter().GetResult();
        _client = factory.CreateClient();
    }

    // --- Seed helper ---

    private async Task<User> SeedUserAsync(
        string username,
        string rawPassword,
        UserRole role,
        bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Check if already seeded (tests share the container — avoid duplicate key)
        var existing = db.Users.FirstOrDefault(u => u.Username == username);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        var user = User.Create(
            UserId.New(),
            username,
            $"Test {username}",
            PasswordHash.Create(hasher.Hash(rawPassword)),
            role,
            now);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Generates a valid test JWT signed with the dev placeholder key.
    /// Used to authenticate requests that require a specific role.
    /// </summary>
    private static string GenerateTestJwt(Guid userId, string username, UserRole role)
    {
        const string signingKey = "campus-connect-dev-placeholder-key-32b";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("unique_name", username),
            new Claim("name", $"Test {username}"),
            new Claim("role", role.ToString()),
            new Claim("schoolId", "SCH-001"),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "campusconnect",
            audience: "campusconnect-clients",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ESC-55 — POST /auth/login happy path returns 200 with token pair

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokens()
    {
        await SeedUserAsync("login_happy", "P@ss1word", UserRole.Docente);

        var response = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "login_happy", password = "P@ss1word" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    // ESC-56 — POST /auth/login with wrong credentials returns 401

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await SeedUserAsync("login_wrong_pw", "P@ss1word", UserRole.Docente);

        var response = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "login_wrong_pw", password = "WrongPwd!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-57 — POST /auth/login with inactive account returns 401
    // Note: User.Deactivate() not yet available — this test seeds an active user to verify other cases
    // The inactive scenario is covered by unit tests (Handle_WithInactiveUser_ReturnsUnauthorized).

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "ghost_user_xyz", password = "any" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-58 — POST /auth/refresh happy path returns 200 with new pair

    [Fact]
    public async Task Refresh_WithValidToken_Returns200AndRotatedPair()
    {
        await SeedUserAsync("refresh_happy", "P@ss1word", UserRole.Docente);
        var loginResponse = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "refresh_happy", password = "P@ss1word" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshResponse = await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        refreshBody!.AccessToken.Should().NotBeNullOrEmpty();
        refreshBody.RefreshToken.Should().NotBe(loginBody.RefreshToken);
    }

    // ESC-59 — POST /auth/refresh replay returns 401

    [Fact]
    public async Task Refresh_WithReplayedToken_Returns401()
    {
        await SeedUserAsync("refresh_replay", "P@ss1word", UserRole.Docente);
        var loginResponse = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "refresh_replay", password = "P@ss1word" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Use token once — should succeed
        await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });

        // Replay — should fail
        var replayResponse = await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody.RefreshToken });

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-60 — GET /me with valid Bearer returns 200 from claims

    [Fact]
    public async Task Me_WithValidBearer_Returns200FromClaims()
    {
        var user = await SeedUserAsync("me_user", "P@ss1word", UserRole.Direccion);
        var jwt = GenerateTestJwt(user.Id.Value, "me_user", UserRole.Direccion);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/identity/users/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        body!.UserId.Should().Be(user.Id.Value);
        body.Username.Should().Be("me_user");
        body.Role.Should().Be("Direccion");
    }

    // ESC-61 — GET /me without Authorization returns 401

    [Fact]
    public async Task Me_WithoutAuthorization_Returns401()
    {
        var response = await _client.GetAsync("/api/identity/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-32 — POST /users without token returns 401

    [Fact]
    public async Task RegisterUser_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/identity/users",
            new { username = "new_user", fullName = "New User", password = "P@ss1word!", role = "Docente" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-33 — POST /users with non-Direccion role returns 403

    [Fact]
    public async Task RegisterUser_WithNonDireccionRole_Returns403()
    {
        var docenteUser = await SeedUserAsync("docente_guard_test", "P@ss1word", UserRole.Docente);
        var jwt = GenerateTestJwt(docenteUser.Id.Value, "docente_guard_test", UserRole.Docente);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/users");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        request.Content = JsonContent.Create(new
        {
            username = "should_fail_user",
            fullName = "Should Fail",
            password = "P@ss1word!",
            role = "Docente"
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ESC-22 — POST /users with Direccion role returns 201

    [Fact]
    public async Task RegisterUser_WithDireccionRole_Returns201()
    {
        var adminUser = await SeedUserAsync("admin_register_test", "P@ss1word", UserRole.Direccion);
        var jwt = GenerateTestJwt(adminUser.Id.Value, "admin_register_test", UserRole.Direccion);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/users");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        request.Content = JsonContent.Create(new
        {
            username = $"new_user_{Guid.NewGuid():N}",
            fullName = "New User Via Admin",
            password = "P@ss1word!",
            role = "Docente"
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ESC-62 — Full login → refresh → /me integration flow

    [Fact]
    public async Task FullFlow_LoginThenRefreshThenMe_AllReturn2xx()
    {
        await SeedUserAsync("full_flow_user", "P@ss1word", UserRole.Direccion);

        // Step 1: Login
        var loginResp = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "full_flow_user", password = "P@ss1word" });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

        // Step 2: Refresh
        var refreshResp = await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<LoginResponse>();

        // Step 3: /me with new access token
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/identity/users/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", refreshBody!.AccessToken);
        var meResp = await _client.SendAsync(meRequest);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ESC-63 — Refresh token replay detection

    [Fact]
    public async Task FullFlow_RefreshReplayDetection_SecondUseFails()
    {
        await SeedUserAsync("replay_detect_user", "P@ss1word", UserRole.Docente);
        var loginResp = await _client.PostAsJsonAsync("/api/identity/auth/login",
            new { username = "replay_detect_user", password = "P@ss1word" });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

        // First use — OK
        var first = await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second use — replay detected
        var second = await _client.PostAsJsonAsync("/api/identity/auth/refresh",
            new { refreshToken = loginBody.RefreshToken });
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ESC-64 — POST /users admin guard (all three scenarios)

    [Fact]
    public async Task FullFlow_AdminGuard_AllThreeScenarios()
    {
        var adminUser = await SeedUserAsync("admin_guard_test", "P@ss1word", UserRole.Direccion);
        var docenteUser = await SeedUserAsync("docente_guard_test2", "P@ss1word", UserRole.Docente);

        var adminJwt = GenerateTestJwt(adminUser.Id.Value, "admin_guard_test", UserRole.Direccion);
        var docenteJwt = GenerateTestJwt(docenteUser.Id.Value, "docente_guard_test2", UserRole.Docente);

        // Scenario 1: No token → 401
        var noToken = await _client.PostAsJsonAsync("/api/identity/users",
            new { username = "test_no_token", fullName = "Test", password = "P@ss1!", role = "Docente" });
        noToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Scenario 2: Docente token → 403
        var docenteReq = new HttpRequestMessage(HttpMethod.Post, "/api/identity/users");
        docenteReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", docenteJwt);
        docenteReq.Content = JsonContent.Create(new { username = "test_docente", fullName = "Test", password = "P@ss1!", role = "Docente" });
        var docenteResp = await _client.SendAsync(docenteReq);
        docenteResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Scenario 3: Direccion token → 201
        var adminReq = new HttpRequestMessage(HttpMethod.Post, "/api/identity/users");
        adminReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminJwt);
        adminReq.Content = JsonContent.Create(new { username = $"test_admin_{Guid.NewGuid():N}", fullName = "Test Admin", password = "P@ss1word!", role = "Docente" });
        var adminResp = await _client.SendAsync(adminReq);
        adminResp.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
