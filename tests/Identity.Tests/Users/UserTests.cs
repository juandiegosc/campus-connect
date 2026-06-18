using BuildingBlocks.Domain.Exceptions;
using FluentAssertions;
using Identity.Domain.Users;
using Identity.Domain.Users.Events;
using Xunit;

namespace Identity.Tests.Users;

public sealed class UserTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    private static PasswordHash ValidHash() => PasswordHash.Create("$2a$12$validhashvalue");

    [Fact]
    public void Create_WithValidArguments_ReturnsUserWithGeneratedId()
    {
        // Arrange
        var id = UserId.New();

        // Act
        var user = User.Create(id, "jdoe", "John Doe", ValidHash(), UserRole.Docente, FixedNow);

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().Be(id);
        user.Username.Should().Be("jdoe");
        user.FullName.Should().Be("John Doe");
        user.Role.Should().Be(UserRole.Docente);
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void Create_WithValidArguments_RaisesExactlyOneUserCreatedDomainEvent()
    {
        // Arrange
        var id = UserId.New();

        // Act
        var user = User.Create(id, "jdoe", "John Doe", ValidHash(), UserRole.Secretaria, FixedNow);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.Single().Should().BeOfType<UserCreatedDomainEvent>();
    }

    [Fact]
    public void DomainEvents_ContainsUserCreatedEventWithCorrectProperties()
    {
        // Arrange
        var id = UserId.New();

        // Act
        var user = User.Create(id, "admin", "Admin User", ValidHash(), UserRole.Direccion, FixedNow);

        // Assert
        var evt = user.DomainEvents.OfType<UserCreatedDomainEvent>().Single();
        evt.UserId.Should().Be(id);
        evt.Username.Should().Be("admin");
        evt.Role.Should().Be(UserRole.Direccion);
        evt.OccurredAt.Should().Be(FixedNow);
    }

    [Fact]
    public void Create_WithEmptyUsername_ThrowsDomainException()
    {
        // Act
        var act = () => User.Create(UserId.New(), string.Empty, "John Doe", ValidHash(), UserRole.Docente, FixedNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithUsernameOver64Chars_ThrowsDomainException()
    {
        // Arrange
        var longUsername = new string('a', 65);

        // Act
        var act = () => User.Create(UserId.New(), longUsername, "John Doe", ValidHash(), UserRole.Docente, FixedNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyFullName_ThrowsDomainException()
    {
        // Act
        var act = () => User.Create(UserId.New(), "jdoe", string.Empty, ValidHash(), UserRole.Finanzas, FixedNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithNullPasswordHash_ThrowsDomainException()
    {
        // Act
        var act = () => User.Create(UserId.New(), "jdoe", "John Doe", null!, UserRole.Docente, FixedNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SetsCreatedAtFromProvidedClock()
    {
        // Arrange
        var customTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var user = User.Create(UserId.New(), "jdoe", "John Doe", ValidHash(), UserRole.Secretaria, customTime);

        // Assert
        user.CreatedAt.Should().Be(customTime);
    }
}
