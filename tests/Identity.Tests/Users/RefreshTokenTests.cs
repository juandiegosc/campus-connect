using FluentAssertions;
using Identity.Domain.RefreshTokens;
using Xunit;

namespace Identity.Tests.Users;

/// <summary>
/// Unit tests for the RefreshToken domain entity (REQ-P3-01, ESC-34, ESC-35).
/// All tests are RED until RefreshToken is implemented (Task 3.2).
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private static readonly string ValidToken = "tok-abc-123";
    private static readonly DateTime NowUtc = DateTime.UtcNow;
    private static readonly DateTime FutureExpiry = NowUtc.AddDays(7);

    // ESC-34 — RefreshToken.Issue creates valid entity

    [Fact]
    public void Issue_WithValidArgs_ReturnsEntityWithIsRevokedFalse()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);

        rt.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Issue_WithValidArgs_SetsCreatedAtFromNowUtc()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);

        rt.CreatedAt.Should().Be(NowUtc);
    }

    [Fact]
    public void Issue_WithValidArgs_SetsAllProperties()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);

        rt.Id.Should().NotBe(Guid.Empty);
        rt.Token.Should().Be(ValidToken);
        rt.UserId.Should().Be(ValidUserId);
        rt.ExpiresAt.Should().Be(FutureExpiry);
    }

    [Fact]
    public void Issue_WithExpiredExpiresAt_ThrowsDomainException()
    {
        var pastExpiry = NowUtc.AddDays(-1);

        var act = () => RefreshToken.Issue(ValidUserId, ValidToken, pastExpiry, NowUtc);

        act.Should().Throw<Exception>().WithMessage("*future*");
    }

    [Fact]
    public void Issue_WithEmptyUserId_ThrowsDomainException()
    {
        var act = () => RefreshToken.Issue(Guid.Empty, ValidToken, FutureExpiry, NowUtc);

        act.Should().Throw<Exception>().WithMessage("*UserId*");
    }

    [Fact]
    public void Issue_WithEmptyToken_ThrowsDomainException()
    {
        var act = () => RefreshToken.Issue(ValidUserId, "  ", FutureExpiry, NowUtc);

        act.Should().Throw<Exception>().WithMessage("*Token*");
    }

    // ESC-35 — RefreshToken.Revoke sets IsRevoked

    [Fact]
    public void Revoke_SetsIsRevokedToTrue()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);

        rt.Revoke();

        rt.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsDomainException()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);
        rt.Revoke();

        var act = () => rt.Revoke();

        act.Should().Throw<Exception>().WithMessage("*revoked*");
    }

    // IsActive tests

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);

        rt.IsActive(NowUtc).Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, FutureExpiry, NowUtc);
        rt.Revoke();

        rt.IsActive(NowUtc).Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        var pastExpiry = NowUtc.AddDays(-1);
        // Create with ExpiresAt in the past — need to use a nowUtc even further in the past
        var pastNow = NowUtc.AddDays(-2);
        var rt = RefreshToken.Issue(ValidUserId, ValidToken, pastExpiry, pastNow);

        rt.IsActive(NowUtc).Should().BeFalse();
    }
}
