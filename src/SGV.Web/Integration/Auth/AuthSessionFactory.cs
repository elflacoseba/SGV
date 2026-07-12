using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// DI-backed implementation of <see cref="IAuthSessionFactory"/> used by the
/// cookie sign-in pipeline of <c>SGV.Web</c>.
///
/// Why this is a sealed class instead of a static one (issue #121 fix):
/// the previous static implementation cached a single
/// <see cref="TokenValidationParameters"/> instance at process scope, which
/// made the first host's <see cref="JwtOptions.SigningKey"/> win for every
/// subsequent host in the test suite. With this class registered as a
/// singleton the options are resolved per-host via <see cref="IOptions{TOptions}"/>
/// and the validation parameters are rebuilt on every invocation against the
/// host's own snapshot — no cross-host contamination is possible.
/// </summary>
internal sealed class AuthSessionFactory(
    ILogger<AuthSessionFactory> logger,
    IOptions<JwtOptions> jwtOptions) : IAuthSessionFactory
{
    /// <inheritdoc />
    public ClaimsPrincipal CreatePrincipal(LoginRequest request, LoginResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(jwtOptions?.Value);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserNameOrEmail),
            new(ClaimTypes.Name, request.UserNameOrEmail)
        };

        AddValidatedTokenClaims(logger, jwtOptions.Value, response.AccessToken, claims);

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <inheritdoc />
    public AuthenticationProperties CreateProperties(LoginResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var properties = new AuthenticationProperties
        {
            ExpiresUtc = response.ExpiresAt,
            IsPersistent = false,
            AllowRefresh = false
        };

        properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = AuthTokenNames.AccessToken, Value = response.AccessToken },
            new AuthenticationToken { Name = AuthTokenNames.ExpiresAt, Value = response.ExpiresAt.ToString("O") }
        });

        return properties;
    }

    private void AddValidatedTokenClaims(ILogger logger, JwtOptions jwtOptions, string accessToken, ICollection<Claim> claims)
    {
        // Construcción por llamada: cada host tiene su propio IServiceProvider y,
        // por extensión, su propio snapshot de IOptions<JwtOptions>. El cache
        // estático quedó eliminado en issue #121 porque contaminaba la suite
        // cuando dos hosts corrían en paralelo con claves distintas.
        var validationParameters = JwtTokenValidationParameters.Create(jwtOptions);
        var principal = new JwtSecurityTokenHandler().ValidateToken(accessToken, validationParameters, out _);

        foreach (var claim in principal.Claims)
        {
            if (claims.Any(existing => existing.Type == claim.Type && existing.Value == claim.Value))
            {
                continue;
            }

            claims.Add(new Claim(claim.Type, claim.Value));
        }

        logger.LogDebug("Access token validated successfully for web cookie session creation.");
    }
}