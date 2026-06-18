using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="User"/>.
/// Maps to the <c>users</c> table with snake_case columns (ESC-16).
/// Stores <see cref="UserRole"/> as <c>varchar(20)</c> (not integer) for readability in Postgres.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");

        // Primary key: UUID mapped from UserId value object
        b.HasKey(u => u.Id);
        b.Property(u => u.Id)
         .HasColumnName("id")
         .HasConversion(id => id.Value, v => UserId.From(v))
         .IsRequired();

        // Username: unique, max 64 chars
        b.Property(u => u.Username)
         .HasColumnName("username")
         .HasMaxLength(64)
         .IsRequired();

        b.HasIndex(u => u.Username)
         .IsUnique()
         .HasDatabaseName("ix_users_username");

        // Full name: max 200 chars
        b.Property(u => u.FullName)
         .HasColumnName("full_name")
         .HasMaxLength(200)
         .IsRequired();

        // Password hash: stored as varchar(256); converted from/to PasswordHash VO
        b.Property(u => u.PasswordHash)
         .HasColumnName("password_hash")
         .HasMaxLength(256)
         .IsRequired()
         .HasConversion(
             ph => ph.Value,
             v => PasswordHash.Create(v));

        // Role: stored as varchar(20) string (not integer), readable in Postgres
        b.Property(u => u.Role)
         .HasColumnName("role")
         .HasMaxLength(20)
         .HasConversion<string>()
         .IsRequired();

        // Active flag with DB-level default
        b.Property(u => u.IsActive)
         .HasColumnName("is_active")
         .HasDefaultValue(true)
         .IsRequired();

        // Creation timestamp in UTC (timestamptz = timestamp with time zone in Postgres)
        b.Property(u => u.CreatedAt)
         .HasColumnName("created_at")
         .HasColumnType("timestamptz")
         .IsRequired();

        // Exclude domain events collection from EF mapping (not a persisted property)
        b.Ignore(u => u.DomainEvents);
    }
}
