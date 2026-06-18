using BuildingBlocks.Application.Common;
using FluentAssertions;
using Identity.Application.Abstractions;
using Identity.Application.Users.Login;
using Identity.Application.Users.Refresh;
using Identity.Domain.RefreshTokens;
using Identity.Domain.Users;
using Xunit;

namespace Identity.Tests.Users;

/// <summary>
/// Unit tests for <see cref="RefreshTokenCommandHandler"/> (REQ-P3-05, ESC-42–45).
/// Tests are RED until RefreshTokenCommandHandler is implemented (Task 5.4).
/// </summary>
public sealed class RefreshTokenCommandHandlerTests
{
    // --- Fakes ---

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public void Seed(User user) => _users.Add(user);

        public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct)
            => Task.FromResult(_users.Any(u => u.Username == username));

        public Task AddAsync(User user, CancellationToken ct)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> FindByUsernameAsync(string username, CancellationToken ct)
            => Task.FromResult(_users.FirstOrDefault(u => u.Username == username));

        public Task<User?> FindByIdAsync(UserId id, CancellationToken ct)
            => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string raw) => $"hashed:{raw}";
        public bool Verify(string raw, string hash) => hash == $"hashed:{raw}";
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        private int _counter;
        public TimeSpan RefreshTokenLifetime { get; } = TimeSpan.FromDays(7);

        public AccessTokenResult CreateAccessToken(User user)
            => new($"access-{_counter++}-{user.Username}", DateTime.UtcNow.AddHours(1));

        public string CreateRefreshToken() => $"new-refresh-{Guid.NewGuid()}";
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _tokens = [];
        public IReadOnlyList<RefreshToken> All => _tokens;

        public void Seed(RefreshToken token) => _tokens.Add(token);

        public Task AddAsync(RefreshToken token, CancellationToken ct)
        {
            _tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct)
            => Task.FromResult(_tokens.FirstOrDefault(rt => rt.Token == token));
    }

    // --- Helpers ---

    private readonly FakeUserRepository _userRepo = new();
    private readonly FakeJwtTokenService _jwtService = new();
    private readonly FakeRefreshTokenRepository _refreshRepo = new();
    private readonly DateTime _nowUtc = DateTime.UtcNow;

    private RefreshTokenCommandHandler CreateHandler()
        => new(_refreshRepo, _userRepo, _jwtService, TimeProvider.System);

    private (User user, RefreshToken refreshToken) SeedActiveRefreshToken(
        string tokenValue = "tok-abc",
        bool isRevoked = false,
        DateTime? expiresAt = null)
    {
        var hasher = new FakePasswordHasher();
        var user = User.Create(
            UserId.New(), "teacher1", "Test User",
            PasswordHash.Create(hasher.Hash("P@ssw0rd")),
            UserRole.Docente,
            _nowUtc);
        _userRepo.Seed(user);

        var expiry = expiresAt ?? _nowUtc.AddDays(7);
        // Use a past 'now' if expiresAt is in the past, so Issue doesn't throw
        var issueNow = expiry > _nowUtc ? _nowUtc : expiry.AddDays(-1);
        var rt = RefreshToken.Issue(user.Id.Value, tokenValue, expiry, issueNow);
        if (isRevoked) rt.Revoke();
        _refreshRepo.Seed(rt);

        return (user, rt);
    }

    // ESC-42 — Refresh happy path rotates tokens

    [Fact]
    public async Task Handle_WithValidToken_ReturnsSuccessWithNewPair()
    {
        SeedActiveRefreshToken("tok-abc");
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithValidToken_RevokesOldToken()
    {
        var (_, rt) = SeedActiveRefreshToken("tok-abc");
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");

        await handler.Handle(cmd, CancellationToken.None);

        rt.IsRevoked.Should().BeTrue();
    }

    // ESC-43 — Refresh replay detection returns Unauthorized

    [Fact]
    public async Task Handle_WithRevokedToken_ReturnsUnauthorized()
    {
        SeedActiveRefreshToken("tok-abc", isRevoked: true);
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ESC-44 — Expired refresh token returns Unauthorized

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsUnauthorized()
    {
        SeedActiveRefreshToken("tok-abc", expiresAt: _nowUtc.AddDays(-1));
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ESC-45 — Unknown refresh token returns Unauthorized

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsUnauthorized()
    {
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("ghost-token");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // Happy path side effects

    [Fact]
    public async Task Handle_HappyPath_PersistsNewRefreshToken()
    {
        SeedActiveRefreshToken("tok-abc");
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");
        var initialCount = _refreshRepo.All.Count;

        await handler.Handle(cmd, CancellationToken.None);

        _refreshRepo.All.Count.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task Handle_HappyPath_NewRefreshTokenIsDifferentFromInput()
    {
        SeedActiveRefreshToken("tok-abc");
        var handler = CreateHandler();
        var cmd = new RefreshTokenCommand("tok-abc");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Value.RefreshToken.Should().NotBe("tok-abc");
    }
}
