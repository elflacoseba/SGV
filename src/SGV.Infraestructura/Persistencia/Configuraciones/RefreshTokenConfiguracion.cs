using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Persistencia.Configuraciones;

/// <summary>
/// EF Core mapping for <see cref="RefreshTokenEntity"/>. Mirrors the
/// shape fixed by REQ-RTM-STORE-1 (spec block B) and the design
/// sections §4 and §8.6:
/// <list type="bullet">
/// <item>Table: <c>RefreshTokens</c>.</item>
/// <item>PK: <c>Id</c> (<c>char(36)</c>) inherited from <c>EntityBase</c>.</item>
/// <item><c>UserId</c>: <c>varchar(450)</c>, FK to <c>AspNetUsers.Id</c> with <c>ON DELETE CASCADE</c>.</item>
/// <item><c>FamilyId</c>: <c>char(36)</c>.</item>
/// <item><c>TokenHash</c>: <c>varchar(64)</c> (SHA-256 hex fits exactly; <c>TokenHash</c> contains the substring <c>Token</c> so <c>AuditoriaSaveChangesInterceptor.EsCampoSensible</c> excludes it from the audit payload — REQ-RTM-AUDIT-1).</item>
/// <item><c>ReplacedById</c>: nullable <c>char(36)</c>, self-FK to <c>RefreshTokens.Id</c> with <c>ON DELETE RESTRICT</c>. The field name intentionally avoids the substring <c>Token</c> so the audit interceptor still records the rotation link — per the design correction (observation #1868, "audit interceptor filters <c>ReplacedByTokenId</c> out by name").</item>
/// <item>Indexes: UNIQUE <c>IX_RefreshTokens_TokenHash</c>, <c>IX_RefreshTokens_UserId</c>, <c>IX_RefreshTokens_FamilyId</c>.</item>
/// <item>Datetime precision: <c>datetime(6)</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// The migration itself is PR1b; this class only ships the fluent
/// mapping. PR1b will run <c>dotnet ef migrations add AddRefreshTokens</c>
/// and lock the DDL against <c>docs/migracion-add-refresh-tokens.sql</c>
/// so the reviewer can audit the schema. Default Pomelo charset is
/// <c>utf8mb4</c> per the rest of the schema, so no explicit
/// <c>HasCharSet</c> is set on the table builder.
/// </remarks>
public sealed class RefreshTokenConfiguracion : IEntityTypeConfiguration<RefreshTokenEntity>
{
    public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.ConfigurarId();

        builder.Property(e => e.UserId)
            .HasColumnType("varchar(450)")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(e => e.FamilyId)
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.LastUsedAt)
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.ReplacedById)
            .HasColumnType("char(36)");

        // FK to AspNetUsers. OnDelete(Cascade) means a user delete
        // purges the family's tokens (matches REQ-RTM-STORE-1 and the
        // generic cascade behaviour of the rest of the system).
        builder.HasOne<SgvIdentityUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RefreshTokens_AspNetUsers_UserId");

        // Self-FK on the rotation link. Restrict guards against an
        // accidental delete of a still-referenced historical row.
        builder.HasOne<RefreshTokenEntity>()
            .WithMany()
            .HasForeignKey(e => e.ReplacedById)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RefreshTokens_RefreshTokens_ReplacedById");

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasIndex(e => e.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId");
    }
}
