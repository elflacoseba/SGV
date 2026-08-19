namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de un refresh token. Modela la fila de la tabla
/// <c>RefreshTokens</c> introducida por PR1a del change
/// <c>implementa-refresh-tokens</c>. La revocación se modela con
/// <see cref="RevokedAt"/> explícito (no es soft-delete sobre
/// <c>IsDeleted</c>).
/// </summary>
/// <remarks>
/// Req. covered: REQ-RTM-ENTITY-1 (spec block B). PK heredada de
/// <see cref="EntityBase"/> es <c>Guid</c> (no <c>long</c>) porque el
/// interceptor de auditoría <c>AuditoriaSaveChangesInterceptor</c>
/// sólo captura <c>EntityBase</c> — cambiar a una PK numérica cortaría
/// el contrato de observabilidad (decisión cerrada C-1 del design
/// §8.6). PR1a no incluye migración; el constraint UNIQUE/FK y los
/// índices viven en <see cref="Configuraciones.RefreshTokenConfiguracion"/>.
/// </remarks>
public class RefreshTokenEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public DateTime LastUsedAt { get; set; }

    /// <summary>
    /// Typed factory used by the persistence-to-domain mapping path
    /// and by tests. Bypasses constructor validation, mirroring the
    /// <c>Reconstitute</c> pattern of <c>Dominio</c> (REQ-124-1).
    /// </summary>
    internal static RefreshTokenEntity Reconstitute(
        Guid id,
        string userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt,
        DateTime? revokedAt,
        Guid? replacedById,
        DateTime lastUsedAt)
    {
        return new RefreshTokenEntity
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            ReplacedById = replacedById,
            LastUsedAt = lastUsedAt,
        };
    }

    /// <summary>
    /// Returns <c>true</c> when the token has not been revoked and has
    /// not yet expired. Boundary: <paramref name="nowUtc"/> &gt;= <see cref="ExpiresAt"/>
    /// is treated as expired to keep the predicate in lock-step with
    /// the repository's <c>WHERE ExpiresAt &gt; @now</c> row-lock query.
    /// </summary>
    public bool IsValid(DateTime nowUtc)
    {
        return RevokedAt is null && ExpiresAt > nowUtc;
    }
}
