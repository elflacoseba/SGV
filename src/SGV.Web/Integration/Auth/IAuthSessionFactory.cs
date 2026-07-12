using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Service responsible for building the cookie-auth artefacts
/// (<see cref="ClaimsPrincipal"/> and <see cref="AuthenticationProperties"/>)
/// that <c>SGV.Web</c> needs to issue after a successful login.
///
/// The interface exists so the factory can be consumed through DI and every
/// host gets its own instance. This is the structural fix for the issue #121
/// regression: the previous static <c>AuthSessionFactory</c> held a process-wide
/// <c>TokenValidationParameters</c> cache that bled configuration across hosts
/// in the test suite. With this contract the cache is gone — each invocation
/// builds fresh <see cref="Microsoft.IdentityModel.Tokens.TokenValidationParameters"/>
/// from the host's <see cref="JwtOptions"/>.
/// </summary>
public interface IAuthSessionFactory
{
    /// <summary>
    /// Builds a <see cref="ClaimsPrincipal"/> by validating the supplied
    /// <see cref="LoginResponse.AccessToken"/> against the host's
    /// <see cref="JwtOptions"/> and merging the validated claims with the
    /// identity claims derived from <see cref="LoginRequest.UserNameOrEmail"/>.
    /// </summary>
    /// <param name="request">Original login request carrying the username or email.</param>
    /// <param name="response">Successful login response carrying the access token and its expiry.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> authenticated under the cookie scheme.</returns>
    /// <exception cref="Microsoft.IdentityModel.Tokens.SecurityTokenException">
    /// The token signature, issuer, audience or lifetime is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">The token is malformed.</exception>
    ClaimsPrincipal CreatePrincipal(LoginRequest request, LoginResponse response);

    /// <summary>
    /// Builds an <see cref="AuthenticationProperties"/> payload for the cookie
    /// auth ticket. Stores the JWT and its absolute expiration under the names
    /// declared in <see cref="AuthTokenNames"/> so the downstream
    /// <c>ApiBearerTokenHandler</c> can bridge the token into API calls.
    /// </summary>
    /// <param name="response">Successful login response carrying the access token and its expiry.</param>
    AuthenticationProperties CreateProperties(LoginResponse response);
}