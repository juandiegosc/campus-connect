using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Analytics.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Hardcoded localhost connection — avoids running the host during dotnet ef migrations add.
/// </summary>
public sealed class AnalyticsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql("Host=localhost;Port=5438;Database=analytics_db;Username=campus;Password=campus")
            .Options;

        // Pass null for IPublisher — not needed for migration generation.
        return new AnalyticsDbContext(opts, null!);
    }
}
