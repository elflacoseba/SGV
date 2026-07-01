using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SGV.Aplicacion.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

internal static class AuthSessionFactory
{
    public static ClaimsPrincipal CreatePrincipal(LoginRequest request, LoginResponse response)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserNameOrEmail),
            new(ClaimTypes.Name, request.UserNameOrEmail)
        };

        TryAddTokenClaims(response.AccessToken, claims);

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

    private static void TryAddTokenClaims(string accessToken, ICollection<Claim> claims)
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
        catch
        {
            // Opaque tokens are acceptable in tests; keep the session usable.
        }
    }
}
