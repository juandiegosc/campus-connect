using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain.Obligations;

namespace Payments.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IObligationRepository.
/// NEVER calls SaveChanges — that is owned by UnitOfWorkBehavior (design §7.1).
/// EF auto-includes OwnsOne Payment for owned types.
/// </summary>
internal sealed class ObligationRepository(PaymentsDbContext ctx) : IObligationRepository
{
    public Task<Obligation?> GetByIdAsync(ObligationId id, CancellationToken ct = default)
        => ctx.Obligations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Obligation>> GetByStatusAsync(ObligationStatus? status, CancellationToken ct = default)
    {
        var query = ctx.Obligations.AsQueryable();
        if (status is not null)
            query = query.Where(o => o.Status == status);
        return await query.ToListAsync(ct);
    }

    public Task AddAsync(Obligation obligation, CancellationToken ct = default)
    {
        ctx.Obligations.Add(obligation);
        return Task.CompletedTask;
    }

    public void Update(Obligation obligation)
        => ctx.Obligations.Update(obligation);   // mark dirty — UoW commits
}
