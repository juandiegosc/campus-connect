using BuildingBlocks.Domain.Exceptions;
using FluentAssertions;
using Identity.Domain.Users;
using Xunit;

namespace Identity.Tests.Users;

public sealed class PasswordHashTests
{
    [Fact]
    public void Create_WithValidHash_ReturnsInstance()
    {
        // Arrange
        const string hash = "$2a$12$validhashvalue";

        // Act
        var result = PasswordHash.Create(hash);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be(hash);
    }

    [Fact]
    public void Create_WithEmptyString_ThrowsDomainException()
    {
        // Act
        var act = () => PasswordHash.Create(string.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithWhitespace_ThrowsDomainException()
    {
        // Act
        var act = () => PasswordHash.Create("   ");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TwoHashesWithSameValue_AreEqual()
    {
        // Arrange
        const string hash = "$2a$12$somehashvalue";

        // Act
        var first = PasswordHash.Create(hash);
        var second = PasswordHash.Create(hash);

        // Assert
        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }
}
