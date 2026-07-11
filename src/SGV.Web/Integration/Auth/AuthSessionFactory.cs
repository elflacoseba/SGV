using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

internal static class AuthSessionFactory
{
    private static TokenValidationParameters? _cachedValidationParameters;
    private static readonly object _cacheLock = new();

    public static ClaimsPrincipal CreatePrincipal(ILogger logger, JwtOptions jwtOptions, LoginRequest request, LoginResponse response)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(jwtOptions);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserNameOrEmail),
            new(ClaimTypes.Name, request.UserNameOrEmail)
        };

        AddValidatedTokenClaims(logger, jwtOptions, response.AccessToken, claims);

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

    private static void AddValidatedTokenClaims(ILogger logger, JwtOptions jwtOptions, string accessToken, ICollection<Claim> claims)
    {
        var validationParameters = GetOrCreateValidationParameters(jwtOptions);
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

    private static TokenValidationParameters GetOrCreateValidationParameters(JwtOptions jwtOptions)
    {
        if (_cachedValidationParameters is null)
        {
            lock (_cacheLock)
            {
                _cachedValidationParameters ??= JwtTokenValidationParameters.Create(jwtOptions);
            }
        }

        return _cachedValidationParameters;
    }
}
