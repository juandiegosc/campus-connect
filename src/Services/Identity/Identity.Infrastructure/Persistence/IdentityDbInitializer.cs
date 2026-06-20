using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Seeds the identity_db with one default user per role on first boot.
/// Idempotent: skips entirely when any user already exists in the database.
/// Default password for all seed users: <c>Admin1234!</c>
/// </summary>
public static class IdentityDbInitializer
{
    private const string DefaultPassword = "Admin1234!";

    private static readonly (string Username, string FullName, UserRole Role)[] SeedUsers =
    [
        ("director1",   "Director Principal",  UserRole.Direccion),
        ("secretaria1", "Secretaria Ejemplo",  UserRole.Secretaria),
        ("finanzas1",   "Analista Finanzas",   UserRole.Finanzas),
        ("docente1",    "Docente Ejemplo",      UserRole.Docente),
    ];

    /// <summary>
    /// Seeds the database. Pass this method as the <c>seeder</c> argument to
    /// <c>app.SeedDatabase(IdentityDbInitializer.Seed)</c> in Program.cs.
    /// </summary>
    /// <param name="sp">Scoped service provider from the startup pipeline.</param>
    public static void Seed(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<IdentityDbContext>();

        if (db.Users.Any())
            return;

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var time   = sp.GetRequiredService<TimeProvider>();
        var now    = time.GetUtcNow().UtcDateTime;

        foreach (var (username, fullName, role) in SeedUsers)
        {
            var hash = PasswordHash.Create(hasher.Hash(DefaultPassword));
            var user = User.Create(UserId.New(), username, fullName, hash, role, now);
            db.Users.Add(user);
        }

        db.SaveChanges();
    }
}
