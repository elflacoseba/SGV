namespace SGV.Aplicacion.Seguridad.Contratos;

/// <summary>
/// Puerto de persistencia de refresh tokens. Trabaja sobre el snapshot
/// inmutable <see cref="RefreshTokenSnapshot"/> para no filtrar tipos de
/// EF Core hacia la capa de aplicación.
/// </summary>
/// <remarks>
/// PR1b (change <c>implementa-refresh-tokens</c>) introduce la primitiva
/// de persistencia que respalda el flujo de rotación/revocación del
/// refresh token. La interfaz vive en <c>SGV.Aplicacion</c> para
/// preservar el boundary Clean Architecture — la implementación EF Core
/// vive en <c>SGV.Infraestructura.Persistencia.Repositorios</c>.
///
/// Decisiones de diseño (design §2.2):
/// <list type="bullet">
///   <item>No hereda <c>IReadOnlyRepository&lt;T&gt;</c> — es un repositorio
///         de escritura sobre una entidad de persistencia, no una
///         proyección de dominio.</item>
///   <item><see cref="TryConsumeAsync"/> devuelve <c>bool</c> en vez de
///         lanzar porque "perder la carrera" es un caso de negocio
///         esperado (replay), no una excepción.</item>
///   <item>No lanza excepciones propias — los errores de transporte de
///         MySQL se propagan como <c>DbUpdateException</c> /
///         <c>MySqlException</c> sin envolver, igual que el resto de los
///         repositorios del repo.</item>
/// </list>
/// </remarks>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Persiste un token nuevo (login o rotación). El caller es responsable
    /// de asignar el <see cref="RefreshTokenSnapshot.Id"/> antes de invocar.
    /// </summary>
    Task AddAsync(RefreshTokenSnapshot token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el token por <see cref="RefreshTokenSnapshot.TokenHash"/>, o
    /// <c>null</c> si no existe.
    /// </summary>
    Task<RefreshTokenSnapshot?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim atómico: marca el token como consumido sólo si sigue activo y
    /// no expirado. Devuelve <c>true</c> si esta llamada ganó la carrera.
    /// Es la primitiva de concurrencia (ver design §2.4).
    /// </summary>
    /// <remarks>
    /// Si el token está revocado o expirado, el UPDATE condicional no
    /// matchea filas y devuelve <c>false</c> sin mutar la fila. Si el
    /// caller necesita distinguir "revocado" vs. "expirado", debe
    /// consultar <see cref="GetByHashAsync"/> después.
    /// </remarks>
    Task<bool> TryConsumeAsync(
        string tokenHash,
        Guid replacedById,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca todas las filas activas de la familia. Devuelve el número de
    /// filas afectadas.
    /// </summary>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca todas las familias activas del usuario (logout sin cookie).
    /// Devuelve el número de filas afectadas.
    /// </summary>
    Task<int> RevokeAllForUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot inmutable de una fila de <c>RefreshTokens</c>. Vive en
/// <c>SGV.Aplicacion</c> para que la capa de aplicación pueda hablar de
/// tokens sin tocar <c>RefreshTokenEntity</c> (que vive en
/// <c>SGV.Infraestructura</c>).
/// </summary>
/// <remarks>
/// PR1b: parte del change <c>implementa-refresh-tokens</c>. La capa de
/// aplicación trabaja exclusivamente con este record — nunca con la entity
/// directamente. El mapeo Entity↔Snapshot vive en
/// <c>RefreshTokenRepository</c> y queda cubierto por los tests de
/// integración del repositorio.
/// </remarks>
public sealed record RefreshTokenSnapshot(
    Guid Id,
    string UserId,
    Guid FamilyId,
    string TokenHash,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    Guid? ReplacedById,
    DateTime LastUsedAt)
{
    /// <summary>
    /// <c>true</c> cuando el token no ha sido revocado y su
    /// <see cref="ExpiresAt"/> es estrictamente mayor que <paramref name="nowUtc"/>.
    /// El comparador estricto reproduce la cláusula <c>WHERE ExpiresAt &gt; @now</c>
    /// que <c>RefreshTokenRepository.TryConsumeAsync</c> usa en el
    /// UPDATE condicional, manteniendo predicate y query en lock-step.
    /// </summary>
    public bool IsActive(DateTime nowUtc) => RevokedAt is null && ExpiresAt > nowUtc;
}