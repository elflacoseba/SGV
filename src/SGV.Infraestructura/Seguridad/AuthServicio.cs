using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Aplicacion.Seguridad.Usuarios;

namespace SGV.Infraestructura.Seguridad;

public sealed class AuthServicio(
    UserManager<SgvIdentityUser> userManager,
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

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false);
        if (!validPassword)
        {
            return null;
        }

        var jwt = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.TokenLifetimeMinutes);
        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("persona_id", user.PersonaId.ToString())
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
