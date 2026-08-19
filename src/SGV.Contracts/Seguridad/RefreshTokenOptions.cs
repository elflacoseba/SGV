namespace SGV.Contracts.Seguridad;

/// <summary>
/// Bound configuration for the refresh token flow (change
/// <c>implementa-refresh-tokens</c>, design §2.5 and REQ-RTM-LIFETIME-1).
/// </summary>
/// <remarks>
/// The lifetime is absolute: <c>ExpiresAt = CreatedAt + RefreshTokenLifetimeDays</c>.
/// Sliding expiration is deliberately out of scope for v1 so that expiry
/// stays predictable and auditable.
/// </remarks>
public sealed class RefreshTokenOptions
{
    /// <summary>
    /// Configuration section name bound in <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Absolute lifetime, in days, of an issued refresh token. Default 14.
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 14;

    /// <summary>
    /// Permitted requests per window for the <c>Refresh</c> rate-limit
    /// policy. Default 20 — more permissive than <c>ChangePassword</c> (5)
    /// because refreshing is a legitimate recurring operation.
    /// </summary>
    public int RateLimitPermitLimit { get; set; } = 20;

    /// <summary>
    /// Fixed window, in minutes, of the <c>Refresh</c> rate-limit policy.
    /// Default 15.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 15;
}
