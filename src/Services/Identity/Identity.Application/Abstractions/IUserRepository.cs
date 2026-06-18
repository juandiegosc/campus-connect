using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

/// <summary>
/// Port for User persistence. Implemented by <c>UserRepository</c> in Identity.Infrastructure.
/// Does NOT expose SaveChangesAsync — persistence is committed by the UnitOfWorkBehavior
/// kernel pipeline behavior (ADR-006).
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Returns <c>true</c> if a user with the given <paramref name="username"/> already exists.
    /// </summary>
    /// <param name="username">The username to check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct);

    /// <summary>
    /// Adds the <paramref name="user"/> to the EF Core change tracker.
    /// The actual INSERT is committed when the UnitOfWorkBehavior calls <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="user">The aggregate root to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(User user, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="User"/> with the given <paramref name="username"/>, or <c>null</c> if not found.
    /// </summary>
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="User"/> with the given <paramref name="id"/>, or <c>null</c> if not found.
    /// </summary>
    Task<User?> FindByIdAsync(UserId id, CancellationToken ct);
}
