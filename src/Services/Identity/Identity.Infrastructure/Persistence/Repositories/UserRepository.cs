using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// Does NOT call SaveChangesAsync — the UnitOfWorkBehavior kernel pipeline handles it.
/// </summary>
internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct)
        => context.Users.AnyAsync(u => u.Username == username, ct);

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken ct)
        => await context.Users.AddAsync(user, ct);

    /// <inheritdoc />
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct)
        => context.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    /// <inheritdoc />
    public Task<User?> FindByIdAsync(UserId id, CancellationToken ct)
        => context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
}
