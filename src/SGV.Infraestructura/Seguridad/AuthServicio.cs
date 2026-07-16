using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;

namespace SGV.Infraestructura.Seguridad;

public sealed class AuthServicio(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext dbContext,
    IOptions<JwtOptions> options) : IAuthServicio
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

        var jwt = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.TokenLifetimeMinutes);
        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var persona = await dbContext.Personas
            .FirstOrDefaultAsync(p => p.Id == user.PersonaId, cancellationToken)
            .ConfigureAwait(false);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("persona_id", user.PersonaId.ToString()),
            new("nombres", persona?.Nombres ?? string.Empty),
            new("apellidos", persona?.Apellidos ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}