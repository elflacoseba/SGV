namespace SGV.Aplicacion.Seguridad.Contratos;

/// <summary>
/// Refresh token lifecycle service (change <c>implementa-refresh-tokens</c>,
/// PR2a). Owns issuance at login, single-use rotation on refresh, replay
/// detection with family revocation, and revocation on logout.
/// </summary>
/// <remarks>
/// The plain refresh token only ever crosses this boundary in memory: the
/// store keeps the SHA-256 digest (REQ-RTM-HASH-1). Callers are responsible
/// for returning the plain value to the client exactly once.
/// </remarks>
public interface IRefreshTokenServicio
{
    /// <summary>
    /// Issues a brand-new refresh token in a brand-new family for
    /// <paramref name="userId"/> (REQ-RTM-FAMILY-1: one family per login).
    /// </summary>
    Task<RefreshTokenEmitido> IssueAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and rotates <paramref name="plainToken"/>. Returns the new
    /// access/refresh pair on success; otherwise the discriminated failure
    /// reason (REQ-AUTH-REFRESH-1..3).
    /// </summary>
    Task<RefreshResult> RefreshAsync(string? plainToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active refresh token of <paramref name="userId"/>.
    /// <paramref name="plainToken"/> is optional and only used to enrich the
    /// audit trail with the family that initiated the logout; its absence is
    /// never an error (REQ-AUTH-LOGOUT-1, legacy-session scenario).
    /// </summary>
    Task RevokeAsync(string userId, string? plainToken = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain refresh token handed back to the caller after issuance, together
/// with its absolute expiration and the family it belongs to.
/// </summary>
public sealed record RefreshTokenEmitido(string Token, DateTimeOffset ExpiresAt, Guid FamilyId);

/// <summary>
/// Discriminated outcome of <see cref="IRefreshTokenServicio.RefreshAsync"/>.
/// </summary>
public enum RefreshOutcome
{
    /// <summary>Rotation succeeded; a new pair was issued.</summary>
    Success,

    /// <summary>The presented token does not exist (or was empty).</summary>
    Invalid,

    /// <summary>
    /// The presented token exists but is past its absolute expiration. The
    /// row is NOT mutated and the family is NOT revoked (REQ-AUTH-REFRESH-2).
    /// </summary>
    Expired,

    /// <summary>
    /// The presented token was already consumed or revoked. The whole family
    /// is revoked server-side (REQ-AUTH-REFRESH-3 / REQ-RTM-REPLAY-1).
    /// </summary>
    ReplayDetected
}

/// <summary>
/// Result of a refresh attempt. Every token-bearing property is populated
/// only when <see cref="Outcome"/> is <see cref="RefreshOutcome.Success"/>.
/// </summary>
public sealed record RefreshResult(
    RefreshOutcome Outcome,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAt = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null)
{
    /// <summary>Failure result carrying no tokens.</summary>
    public static RefreshResult Failure(RefreshOutcome outcome) => new(outcome);
}
