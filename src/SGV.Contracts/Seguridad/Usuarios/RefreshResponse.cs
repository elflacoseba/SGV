namespace SGV.Contracts.Seguridad.Usuarios;

/// <summary>
/// Successful response of <c>POST /api/v1/auth/refresh</c>. Carries the
/// freshly minted access JWT, its absolute expiration, the rotated
/// refresh token, and that refresh token's absolute expiration. Wire
/// naming follows the camelCase convention of the API
/// (<c>accessToken</c>, <c>expiresAt</c>, <c>refreshToken</c>,
/// <c>refreshTokenExpiresAt</c>) — see
/// <c>RefreshContractsSerializationTests</c>.
/// </summary>
public sealed record RefreshResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
