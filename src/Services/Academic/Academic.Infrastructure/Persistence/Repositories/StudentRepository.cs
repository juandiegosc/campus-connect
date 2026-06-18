using Academic.Application.Abstractions;
using Academic.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Academic.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStudentRepository"/>.
/// Does NOT call SaveChanges — that is handled by UnitOfWorkBehavior in the MediatR pipeline.
/// </summary>
internal sealed class StudentRepository(AcademicDbContext ctx) : IStudentRepository
{
    public async Task<Student?> GetByIdAsync(StudentId id, CancellationToken ct = default)
        => await ctx.Students
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Student?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => await ctx.Students
            .FirstOrDefaultAsync(s => s.DocumentId == documentId, ct);

    public async Task<bool> ExistsByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
        => await ctx.Students
            .AnyAsync(s => s.DocumentId == documentId, ct);

    public async Task AddAsync(Student student, CancellationToken ct = default)
    {
        await ctx.Students.AddAsync(student, ct);
        // NO SaveChanges — UnitOfWorkBehavior commits the transaction atomically
    }

    public async Task<(IReadOnlyList<Student> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? grade, string? search, CancellationToken ct = default)
    {
        var query = ctx.Students.AsQueryable();

        if (!string.IsNullOrWhiteSpace(grade))
            query = query.Where(s => s.Grade == grade);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.FullName.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
