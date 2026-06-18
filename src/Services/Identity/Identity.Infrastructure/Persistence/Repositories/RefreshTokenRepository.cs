using Identity.Application.Abstractions;
using Identity.Domain.RefreshTokens;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenRepository"/>.
/// IMPORTANT: <see cref="FindByTokenAsync"/> uses tracked queries (no AsNoTracking).
/// The <see cref="RefreshTokenCommandHandler"/> calls <c>Revoke()</c> on the returned entity
/// and relies on EF change tracking to generate the UPDATE (Gotcha G5).
/// </summary>
internal sealed class RefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    /// <inheritdoc />
    public async Task AddAsync(RefreshToken token, CancellationToken ct)
        => await context.Set<RefreshToken>().AddAsync(token, ct);

    /// <inheritdoc />
    public Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct)
        // NO .AsNoTracking() — Revoke() mutation must flow through EF change tracking (Gotcha G5).
        => context.Set<RefreshToken>().FirstOrDefaultAsync(rt => rt.Token == token, ct);
}
