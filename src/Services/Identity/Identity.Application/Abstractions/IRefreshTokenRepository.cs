using Identity.Domain.RefreshTokens;

namespace Identity.Application.Abstractions;

/// <summary>
/// Port for RefreshToken persistence.
/// Implemented by <c>RefreshTokenRepository</c> in Identity.Infrastructure.
/// No EF Core or PostgreSQL types are exposed here (ESC-37).
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Stages a new <see cref="RefreshToken"/> for insertion.
    /// The actual INSERT is committed when the UnitOfWorkBehavior calls <c>SaveChangesAsync</c>.
    /// </summary>
    Task AddAsync(RefreshToken token, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="RefreshToken"/> matching the given <paramref name="token"/> value,
    /// or <c>null</c> if not found.
    /// IMPORTANT: returned entity MUST be EF-tracked so that Revoke() mutation flows to SaveChanges.
    /// </summary>
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct);
}
