using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Academic.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="AcademicDbContext"/>.
/// Required for `dotnet ef migrations add` without a running host (ESC-32).
/// Uses a hardcoded localhost connection string — this is a local-only project.
/// </summary>
public sealed class AcademicDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AcademicDbContext>
{
    public AcademicDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AcademicDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=academic_db;Username=postgres;Password=postgres")
            .Options;

        // Pass null publisher — design-time factory does not need domain event dispatch
        return new AcademicDbContext(opts, null!);
    }
}
