using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Contracts.Seguridad;
using SGV.Infraestructura.Persistencia;

namespace SGV.Infraestructura.Seguridad;

/// <summary>
/// Mints the SGV access JWT. Extracted from <c>AuthServicio</c> in PR2a of
/// change <c>implementa-refresh-tokens</c> so login and refresh share a
/// single claim-assembly implementation.
/// </summary>
/// <remarks>
/// The claim set is intentionally identical to the pre-PR2a login token
/// (design §2.7): <c>sub</c>, <c>NameIdentifier</c>, <c>Name</c>,
/// <c>persona_id</c>, <c>nombres</c>, <c>apellidos</c> and one
/// <c>role</c> claim per assigned role. No <c>jti</c> and no
/// <c>family_id</c> are added: server-side revocation lives in the refresh
/// token family, not in an access-token denylist.
///
/// Roles and persona data are read on every issuance, so a role removed
/// between login and refresh is no longer present in the rotated token.
/// </remarks>
public sealed class JwtAccessTokenIssuer(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext dbContext,
    IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    /// <inheritdoc />
    public async Task<AccessTokenEmitido?> EmitirAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user is null ? null : await EmitirParaAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Issues the JWT for an already-materialized Identity user, avoiding a
    /// redundant lookup on the login path.
    /// </summary>
    internal async Task<AccessTokenEmitido> EmitirParaAsync(
        SgvIdentityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

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

        return new AccessTokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
