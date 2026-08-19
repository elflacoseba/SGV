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
/// <item><c>ReplacedById</c>: nullable <c>char(36)</c> — puntero LÓGICO al Id de la fila que reemplazó a esta. NO tiene FK self-referencing porque la rotación atómica (UPDATE del viejo + INSERT del nuevo) necesita escribir <c>ReplacedById = newId</c> antes de insertar la fila con <c>newId</c>, y MySQL no soporta FKs diferidas. La integridad de la cadena se mantiene vía <c>FamilyId</c> y el índice <c>IX_RefreshTokens_ReplacedById</c>. El nombre evita el substring <c>Token</c> para que el interceptor de auditoría lo registre (corrección del design, observación #1868).</item>
/// <item>Indexes: UNIQUE <c>IX_RefreshTokens_TokenHash</c>, <c>IX_RefreshTokens_UserId</c>, <c>IX_RefreshTokens_FamilyId</c>, <c>IX_RefreshTokens_ReplacedById</c>.</item>
/// <item>Datetime precision: <c>datetime(6)</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// PR1b design deviation (documented in PR body): la spec original y el
/// design §4 planteaban un self-FK <c>FK_RefreshTokens_RefreshTokens_ReplacedById</c>
/// con <c>ON DELETE RESTRICT</c>. Ese constraint entra en conflicto con
/// la primitiva atómica de rotación (<see cref="Repositorios.RefreshTokenRepository.TryConsumeAsync"/>):
/// el UPDATE necesita escribir <c>ReplacedById = newId</c> antes de que
/// la fila con <c>Id = newId</c> exista en la tabla, y MySQL evalúa los
/// FKs de inmediato (no hay <c>DEFERRABLE</c>). La solución adoptada es
/// tratar <c>ReplacedById</c> como un puntero lógico sin enforcement DB —
/// la cadena de rotación se reconstruye vía queries a <c>FamilyId</c> y
/// <c>ReplacedById</c>, y la integridad la garantiza el flujo atómico en
/// <c>RefreshTokenServicio</c> (PR2). El índice se conserva para que las
/// queries de "qué token reemplazó a este" sigan siendo O(log n).
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

        // Self-FK on ReplacedById removida (ver remarks de la clase).
        // El constraint bloqueaba la rotación atómica descrita en
        // design §2.4 y re-litigar esto en el repositorio agregaba
        // complejidad sin beneficio observable.

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasIndex(e => e.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId");

        // El índice se conserva: la rotación atómica puede poblar
        // ReplacedById antes del INSERT de la fila que lo referencia, así
        // que el índice es necesario para que las queries
        // "¿qué token reemplazó a este?" no escaneen la tabla.
        builder.HasIndex(e => e.ReplacedById)
            .HasDatabaseName("IX_RefreshTokens_ReplacedById");
    }
}
