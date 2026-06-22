using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Hardcoded localhost connection — avoids running the host during dotnet ef migrations add.
/// </summary>
public sealed class NotificationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql("Host=localhost;Port=5438;Database=notifications_db;Username=campus;Password=campus")
            .Options;

        // Pass null for IPublisher — not needed for migration generation.
        return new NotificationsDbContext(opts, null!);
    }
}
