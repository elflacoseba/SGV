using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Seguridad;

/// <summary>
/// Defers the construction of <see cref="TokenValidationParameters"/> for
/// <see cref="JwtBearerOptions"/> until DI resolves the validated
/// <see cref="JwtOptions"/>.
///
/// The previous wiring captured <c>IConfiguration</c> via closure at
/// <c>Program</c> registration time, sealing the signing key on a snapshot
/// that ignored validated options. This implementation reads from
/// <see cref="IOptions{TOptions}"/> so any <c>ValidateOnStart</c> failure
/// propagates to host build and any successful bind takes effect here.
/// </summary>
internal sealed class ConfigureJwtBearerFromJwtOptions(
    IOptions<JwtOptions> options) : IPostConfigureOptions<JwtBearerOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, JwtBearerOptions bearer)
    {
        // Defensive guard against a future multi-scheme setup: if another
        // JwtBearer registration appears, leave its TokenValidationParameters
        // untouched so we do not stomp unrelated handlers.
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var jwt = options.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        };
    }
}
