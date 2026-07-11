using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using SGV.Contracts.Seguridad;

namespace SGV.Api.Seguridad;

/// <summary>
/// Defers the construction of bearer token validation parameters for
/// <see cref="JwtBearerOptions"/> until DI resolves the validated
/// <see cref="JwtOptions"/>. The actual parameters are built by
/// <see cref="JwtTokenValidationParameters"/>.
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

        bearer.TokenValidationParameters = JwtTokenValidationParameters.Create(options.Value);
    }
}
