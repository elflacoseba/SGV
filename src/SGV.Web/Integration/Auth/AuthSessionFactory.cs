using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

internal static class AuthSessionFactory
{
    public static ClaimsPrincipal CreatePrincipal(ILogger logger, LoginRequest request, LoginResponse response)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserNameOrEmail),
            new(ClaimTypes.Name, request.UserNameOrEmail)
        };

        TryAddTokenClaims(logger, response.AccessToken, claims);

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static AuthenticationProperties CreateProperties(LoginResponse response)
    {
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

    private static void TryAddTokenClaims(ILogger logger, string accessToken, ICollection<Claim> claims)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            foreach (var claim in jwt.Claims)
            {
                if (claims.Any(existing => existing.Type == claim.Type && existing.Value == claim.Value))
                {
                    continue;
                }

                claims.Add(new Claim(claim.Type, claim.Value));
            }
        }
        catch (Exception ex)
        {
            // Opaque tokens are acceptable in tests; keep the session usable.
            // Still surface the parse failure so admins aren't silently
            // downgraded — without role claims, every Authorize(Roles=...)
            // gate will deny until the user logs in again to obtain a parseable
            // access token.
            logger.LogWarning(ex, "Failed to parse access token claims; admin role checks may fail until re-login.");
        }
    }
}
