using Identity.Domain.RefreshTokens;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="RefreshToken"/>.
/// Maps to table <c>refresh_tokens</c> with snake_case columns (design §5.2, ESC-52).
/// Unique index on <c>token</c> enforces the single-use invariant at the DB level.
/// FK to <c>users(id)</c> with ON DELETE CASCADE (defensive — future User deletion cleans up tokens).
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");

        b.HasKey(rt => rt.Id);
        b.Property(rt => rt.Id)
         .HasColumnName("id");

        b.Property(rt => rt.Token)
         .HasColumnName("token")
         .HasMaxLength(128)
         .IsRequired();
        b.HasIndex(rt => rt.Token)
         .IsUnique()
         .HasDatabaseName("ix_refresh_tokens_token");

        b.Property(rt => rt.UserId)
         .HasColumnName("user_id")
         .IsRequired();
        b.HasIndex(rt => rt.UserId)
         .HasDatabaseName("ix_refresh_tokens_user_id");

        b.Property(rt => rt.ExpiresAt)
         .HasColumnName("expires_at")
         .HasColumnType("timestamptz")
         .IsRequired();

        b.Property(rt => rt.IsRevoked)
         .HasColumnName("is_revoked")
         .HasDefaultValue(false)
         .IsRequired();

        b.Property(rt => rt.CreatedAt)
         .HasColumnName("created_at")
         .HasColumnType("timestamptz")
         .IsRequired();

        // FK constraint at DB level via raw SQL annotation.
        // NOTE: User.Id is a UserId VO with a HasConversion to Guid, while RefreshToken.UserId
        // is a plain Guid. EF cannot reconcile the CLR type mismatch via HasOne/WithMany
        // without explicit principal key alignment. We define the raw FK using a check constraint
        // approach: just ignore the EF navigation and let the migration define the FK directly.
        // This keeps the domain model clean (no navigation property on User) while preserving
        // referential integrity at the DB level via the migration's AddForeignKey call.
        //
        // The migration Up() will manually add:
        //   table.ForeignKey("fk_refresh_tokens_users_user_id", x => x.user_id, "users", "id",
        //       onDelete: ReferentialAction.Cascade);
        // See: AddRefreshTokens migration (generated and then manually verified).
    }
}
