using Payments.Domain.Obligations;

namespace Payments.Application.Abstractions;

/// <summary>
/// Port for Obligation persistence. NO EF Core / Npgsql references.
/// SaveChanges is owned by UnitOfWorkBehavior — repository never calls it.
/// </summary>
public interface IObligationRepository
{
    Task<Obligation?> GetByIdAsync(ObligationId id, CancellationToken ct = default);
    Task<IReadOnlyList<Obligation>> GetByStatusAsync(ObligationStatus? status, CancellationToken ct = default);
    Task AddAsync(Obligation obligation, CancellationToken ct = default);
    void Update(Obligation obligation);   // mark dirty — NO SaveChanges (UoW owns commit)
}
