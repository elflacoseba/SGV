using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Aplicacion.Seguridad.Servicios;
using SGV.Contracts.Seguridad;

namespace SGV.Infraestructura.Seguridad;

/// <summary>
/// Refresh token lifecycle implementation (PR2a of change
/// <c>implementa-refresh-tokens</c>, design §2.3 and §2.4).
/// </summary>
/// <remarks>
/// Concurrency contract: the single atomic conditional UPDATE behind
/// <see cref="IRefreshTokenRepository.TryConsumeAsync"/> is the only
/// serialization point. A caller that loses the race observes
/// <c>false</c> and is treated exactly like a replay, which satisfies
/// REQ-RTM-CONCURRENCY-1 without pessimistic locking.
///
/// Audit contract: <c>ExecuteUpdateAsync</c> bypasses the EF change
/// tracker, so <c>AuditoriaSaveChangesInterceptor</c> never fires for
/// revocations. Every revocation path therefore writes an explicit entry
/// through <see cref="IAuditoriaServicio"/> (risk R5 of the design). The
/// audit payload never carries the plain token nor its digest.
/// </remarks>
public sealed class RefreshTokenServicio(
    IRefreshTokenRepository repository,
    IUnitOfWork unitOfWork,
    IAccessTokenIssuer accessTokenIssuer,
    IAuditoriaServicio auditoria,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options) : IRefreshTokenServicio
{
    /// <summary>Audited logical entity name (matches the EF interceptor naming).</summary>
    private const string EntidadAuditada = "RefreshToken";

    /// <summary>Audit operation recorded when a whole family is revoked by replay.</summary>
    internal const string OperacionRevocarFamilia = "RevocarFamilia";

    /// <summary>Audit operation recorded when a user logs out.</summary>
    internal const string OperacionLogout = "Logout";

    /// <summary>Entropy of the plain refresh token, in bytes.</summary>
    private const int TokenBytes = 32;

    /// <inheritdoc />
    public async Task<RefreshTokenEmitido> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var emitido = await EmitirAsync(userId, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return emitido;
    }

    /// <inheritdoc />
    public async Task<RefreshResult> RefreshAsync(string? plainToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return RefreshResult.Failure(RefreshOutcome.Invalid);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var presentedHash = RefreshTokenHashing.ComputeSha256Hex(plainToken);
        var replacementId = Guid.NewGuid();

        var won = await repository
            .TryConsumeAsync(presentedHash, replacementId, nowUtc, cancellationToken)
            .ConfigureAwait(false);

        if (!won)
        {
            return await ResolverFalloAsync(presentedHash, nowUtc, cancellationToken).ConfigureAwait(false);
        }

        var consumido = await repository
            .GetByHashAsync(presentedHash, cancellationToken)
            .ConfigureAwait(false);
        if (consumido is null)
        {
            // Defensive: the row was consumed a moment ago, so its absence
            // means someone deleted it concurrently. Fail closed.
            return RefreshResult.Failure(RefreshOutcome.Invalid);
        }

        var accessToken = await accessTokenIssuer
            .EmitirAsync(consumido.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (accessToken is null)
        {
            // The user disappeared (hard delete) between issuance and
            // refresh: revoke what is left of the family and fail closed.
            await repository.RevokeFamilyAsync(consumido.FamilyId, nowUtc, cancellationToken).ConfigureAwait(false);
            return RefreshResult.Failure(RefreshOutcome.Invalid);
        }

        var rotado = await EmitirAsync(
                consumido.UserId,
                consumido.FamilyId,
                cancellationToken,
                tokenId: replacementId)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RefreshResult(
            RefreshOutcome.Success,
            accessToken.AccessToken,
            accessToken.ExpiresAt,
            rotado.Token,
            rotado.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(
        string userId,
        string? plainToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        // The presented token is only used to enrich the audit trail; the
        // revocation itself always covers every active family of the user so
        // that logout is a global sign-out (REQ-AUTH-LOGOUT-1).
        Guid? familyId = null;
        if (!string.IsNullOrWhiteSpace(plainToken))
        {
            var presentado = await repository
                .GetByHashAsync(RefreshTokenHashing.ComputeSha256Hex(plainToken), cancellationToken)
                .ConfigureAwait(false);
            if (presentado is not null
                && string.Equals(presentado.UserId, userId, StringComparison.Ordinal))
            {
                familyId = presentado.FamilyId;
            }
        }

        var revocados = await repository
            .RevokeAllForUserAsync(userId, nowUtc, cancellationToken)
            .ConfigureAwait(false);

        if (revocados == 0)
        {
            // Legacy session without refresh tokens: graceful no-op.
            return;
        }

        await auditoria.RegistrarAsync(
                EntidadAuditada,
                userId,
                OperacionLogout,
                userId,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["UserId"] = userId,
                    ["FamilyId"] = familyId?.ToString(),
                    ["RevokedCount"] = revocados,
                    ["RevokedAt"] = nowUtc
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Distinguishes the three reasons a conditional consume can fail:
    /// unknown token, expired token (row untouched, no family revocation per
    /// REQ-AUTH-REFRESH-2) and replay/lost-race (family revoked and audited
    /// per REQ-AUTH-REFRESH-3).
    /// </summary>
    private async Task<RefreshResult> ResolverFalloAsync(
        string presentedHash,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existente = await repository
            .GetByHashAsync(presentedHash, cancellationToken)
            .ConfigureAwait(false);

        if (existente is null)
        {
            return RefreshResult.Failure(RefreshOutcome.Invalid);
        }

        if (existente.RevokedAt is null && existente.ExpiresAt <= nowUtc)
        {
            return RefreshResult.Failure(RefreshOutcome.Expired);
        }

        var revocados = await repository
            .RevokeFamilyAsync(existente.FamilyId, nowUtc, cancellationToken)
            .ConfigureAwait(false);

        await auditoria.RegistrarAsync(
                EntidadAuditada,
                existente.FamilyId.ToString(),
                OperacionRevocarFamilia,
                existente.UserId,
                new Dictionary<string, object?>(StringComparer.Ordinal),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["FamilyId"] = existente.FamilyId,
                    ["UserId"] = existente.UserId,
                    ["Motivo"] = "Replay",
                    ["RevokedCount"] = revocados,
                    ["RevokedAt"] = nowUtc
                },
                cancellationToken)
            .ConfigureAwait(false);

        return RefreshResult.Failure(RefreshOutcome.ReplayDetected);
    }

    /// <summary>
    /// Generates a token, hashes it and stages the row. The caller commits.
    /// </summary>
    private async Task<RefreshTokenEmitido> EmitirAsync(
        string userId,
        Guid familyId,
        CancellationToken cancellationToken,
        Guid? tokenId = null)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = nowUtc.AddDays(options.Value.RefreshTokenLifetimeDays);
        var plain = GenerarToken();

        await repository.AddAsync(
                new RefreshTokenSnapshot(
                    Id: tokenId ?? Guid.NewGuid(),
                    UserId: userId,
                    FamilyId: familyId,
                    TokenHash: RefreshTokenHashing.ComputeSha256Hex(plain),
                    CreatedAt: nowUtc,
                    ExpiresAt: expiresAt,
                    RevokedAt: null,
                    ReplacedById: null,
                    LastUsedAt: nowUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return new RefreshTokenEmitido(plain, new DateTimeOffset(expiresAt, TimeSpan.Zero), familyId);
    }

    /// <summary>
    /// 256 bits of CSPRNG entropy encoded as URL-safe Base64 so the value can
    /// travel in a JSON body and a cookie without escaping.
    /// </summary>
    private static string GenerarToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
