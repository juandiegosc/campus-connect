using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Attendance.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Hardcoded localhost connection — avoids running the host during dotnet ef migrations add.
/// Gotcha 12: if dotnet ef database update fails via SCRAM from host,
/// apply inside container: docker exec -i cc-postgres psql -U campus -d attendance_db &lt; migration.sql
/// </summary>
public sealed class AttendanceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseNpgsql("Host=localhost;Port=5438;Database=attendance_db;Username=campus;Password=campus")
            .Options;

        // Pass null for IPublisher — not needed for migration generation
        return new AttendanceDbContext(opts, null!);
    }
}
