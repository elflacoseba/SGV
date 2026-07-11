using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SGV.Contracts.Seguridad;

/// <summary>
/// Shared builder for <see cref="TokenValidationParameters"/> so that
/// <c>SGV.Api</c> and <c>SGV.Web</c> validate access tokens against
/// the same contract. Keeps issuer, audience, signing-key and lifetime
/// policy (ClockSkew = <see cref="TimeSpan.Zero"/>) in a single place.
/// </summary>
public static class JwtTokenValidationParameters
{
    /// <summary>
    /// Creates a <see cref="TokenValidationParameters"/> instance from the
    /// given <paramref name="options"/>. The returned parameters enforce
    /// signature, issuer, audience and lifetime validation with zero clock
    /// skew.
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
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey))
        };
    }
}
