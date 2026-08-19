using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SGV.Contracts.Seguridad;

/// <summary>
/// Shared builder for <see cref="TokenValidationParameters"/> so that
/// <c>SGV.Api</c> and <c>SGV.Web</c> validate access tokens against
/// the same contract. Keeps issuer, audience, signing-key and lifetime
/// policy (ClockSkew = 30 seconds, see decisiones-implementacion.md)
/// in a single place.
/// </summary>
public static class JwtTokenValidationParameters
{
    /// <summary>
    /// Clock-skew tolerance applied to <c>exp</c>/<c>nbf</c> claims when
    /// validating JWTs. 30 seconds absorbs the typical drift between
    /// hosts in container/VM deployments without admitting meaningful
    /// post-expiration use of tokens.
    /// </summary>
    public static readonly TimeSpan TokenValidationClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a <see cref="TokenValidationParameters"/> instance from the
    /// given <paramref name="options"/>. The returned parameters enforce
    /// signature, issuer, audience and lifetime validation with a 30s
    /// clock-skew tolerance.
    /// </summary>
    public static TokenValidationParameters Create(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TokenValidationClockSkew,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey))
        };
    }
}
