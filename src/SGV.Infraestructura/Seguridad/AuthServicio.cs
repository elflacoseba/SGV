using Microsoft.AspNetCore.Identity;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Infraestructura.Seguridad;

/// <summary>
/// Authenticates credentials and returns the access/refresh pair.
/// </summary>
/// <remarks>
/// PR2a of change <c>implementa-refresh-tokens</c>: claim assembly moved to
/// <see cref="JwtAccessTokenIssuer"/> and a refresh token is now issued on
/// every successful login (one family per login, REQ-RTM-FAMILY-1). The
/// returned <see cref="LoginResponse"/> keeps its first two positional
/// members, so callers that only read the access token are unaffected.
/// </remarks>
public sealed class AuthServicio(
    UserManager<SgvIdentityUser> userManager,
    JwtAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenServicio refreshTokenServicio) : IAuthServicio
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.UserNameOrEmail).ConfigureAwait(false)
            ?? await userManager.FindByEmailAsync(request.UserNameOrEmail).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        // Cambio 2026-07-15-quita-soft-delete-usuario: el chequeo de
        // bloqueo se delega a IsLockedOutAsync (Identity) y se hace
        // ANTES de CheckPasswordAsync para evitar timing leaks y
        // enumeración.
        if (await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return null;
        }

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false);
        if (!validPassword)
        {
            // RIS-001 (4R review): contar el intento fallido vía
            // AccessFailedAsync. Identity aplica MaxFailedAccessAttempts
            // (configurado a 5 en Program.cs IdentityCore) y, al cruzar
            // el umbral, llena LockoutEnd hasta DefaultLockoutTimeSpan.
            // Cuando IsLockedOutAsync pasa a true, devolvemos null igual
            // que para credenciales inválidas — el caller (AuthController)
            // mapea ambos casos a 401. La causa exacta (creds vs lockout)
            // queda distinguible vía AccessFailedCount y LockoutEnd.
            await userManager.AccessFailedAsync(user).ConfigureAwait(false);
            return null;
        }

        // RIS-001 (4R review): resetear AccessFailedCount tras un login
        // exitoso. Sin esto, brute-force continuaría acumulando aún cuando
        // el atacante conociera la password.
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        var accessToken = await accessTokenIssuer
            .EmitirParaAsync(user, cancellationToken)
            .ConfigureAwait(false);

        var refreshToken = await refreshTokenServicio
            .IssueAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        return new LoginResponse(
            accessToken.AccessToken,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt);
    }
}
