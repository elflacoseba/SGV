using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Implementación EF Core de <see cref="IRefreshTokenRepository"/>.
/// PR1b (change <c>implementa-refresh-tokens</c>): persiste y rota
/// refresh tokens en MySQL siguiendo el contrato definido en
/// design §2.2 y §2.4.
///
/// Decisiones críticas:
/// <list type="bullet">
///   <item><see cref="TryConsumeAsync"/> usa <c>ExecuteUpdateAsync</c>
///         atómico — UNA sola sentencia <c>UPDATE ... WHERE
///         TokenHash=@h AND RevokedAt IS NULL AND ExpiresAt &gt; @now</c>
///         garantiza que a lo sumo una llamada concurrente gane la
///         carrera (REQ-RTM-CONCURRENCY-1).</item>
///   <item><see cref="RevokeFamilyAsync"/> y
///         <see cref="RevokeAllForUserAsync"/> también usan
///         <c>ExecuteUpdateAsync</c> — un solo round-trip por operación,
///         sin tracking ni materialización de filas.</item>
///   <item>El interceptor de auditoría (<c>AuditoriaSaveChangesInterceptor</c>)
///         cubre automáticamente el alta vía <see cref="AddAsync"/> +
///         <c>SaveChangesAsync</c>, excluyendo <c>TokenHash</c> por la
///         convención de nombre (REQ-RTM-AUDIT-1). Las revocaciones
///         vía <c>ExecuteUpdateAsync</c> NO disparan el interceptor y
///         deben ser auditadas explícitamente por
///         <c>RefreshTokenServicio</c> (PR2) vía
///         <c>IAuditoriaServicio.RegistrarAsync</c>.</item>
/// </list>
/// </summary>
public sealed class RefreshTokenRepository(SgvDbContext context) : IRefreshTokenRepository
{
    private static RefreshTokenSnapshot Mapear(RefreshTokenEntity entity)
    {
        return new RefreshTokenSnapshot(
            Id: entity.Id,
            UserId: entity.UserId,
            FamilyId: entity.FamilyId,
            TokenHash: entity.TokenHash,
            CreatedAt: entity.CreatedAt,
            ExpiresAt: entity.ExpiresAt,
            RevokedAt: entity.RevokedAt,
            ReplacedById: entity.ReplacedById,
            LastUsedAt: entity.LastUsedAt);
    }

    /// <inheritdoc />
    public async Task AddAsync(RefreshTokenSnapshot token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var entity = new RefreshTokenEntity
        {
            Id = token.Id,
            UserId = token.UserId,
            FamilyId = token.FamilyId,
            TokenHash = token.TokenHash,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            RevokedAt = token.RevokedAt,
            ReplacedById = token.ReplacedById,
            LastUsedAt = token.LastUsedAt,
        };

        await context.RefreshTokens.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenSnapshot?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        var entity = await context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Mapear(entity);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(
        string tokenHash,
        Guid replacedById,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return false;
        }

        // UPDATE condicional atómico: la fila se marca RevokedAt=nowUtc
        // sólo si sigue activa y no expirada. Si otro caller ya ganó la
        // carrera, RevokedAt != null y el WHERE no matchea → affected == 0.
        var affected = await context.RefreshTokens
            .Where(r => r.TokenHash == tokenHash
                        && r.RevokedAt == null
                        && r.ExpiresAt > nowUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.RevokedAt, nowUtc)
                    .SetProperty(r => r.ReplacedById, replacedById)
                    .SetProperty(r => r.LastUsedAt, nowUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await context.RefreshTokens
            .Where(r => r.FamilyId == familyId && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.RevokedAt, nowUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForUserAsync(
        string userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        return await context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.RevokedAt, nowUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }
}