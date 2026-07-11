using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;

namespace SGV.Tests.Web.Common;

/// <summary>
/// Helper compartido por los <see cref="IClassFixture{TFixture}"/> web de los
/// módulos que requieren autenticar con rol <see cref="RolesSgv.Administrador"/>
/// durante los tests de integración (Cargo, Puesto, futuros Habilidad/Persona,
/// etc.).
/// Centraliza la generación del JWT firmado con un HMAC dummy para evitar
/// duplicación literal entre fixtures y para que el comentario justificando
/// la no-validación de firma viva en un solo lugar.
/// </summary>
public static class AdminJwtTestHelper
{
    /// <summary>
    /// Clave HMAC dummy usada para firmar el JWT del "admin" en los tests.
    /// Es fija (no la rotamos entre tests) y NO corresponde a la clave real
    /// de <c>JwtOptions</c>: <see cref="SGV.Web.Integration.Auth.AuthSessionFactory.TryAddTokenClaims"/>
    /// sólo lee los claims del JWT, no valida la firma. El HMAC es suficiente
    /// para que <c>JwtSecurityTokenHandler.WriteToken</c> produzca un token con
    /// la estructura canónica (header.payload.signature).
    /// </summary>
    private const string AdminFixtureSigningKey =
        "sgv-tests-fixture-admin-jwt-signing-key-32bytes-long-enough";

    /// <summary>
    /// Genera un JWT firmado con HMAC-SHA256 que incluye el claim
    /// <see cref="ClaimTypes.Role"/> con valor <see cref="RolesSgv.Administrador"/>.
    /// El issuer/audience coinciden con los configurados en <c>JwtOptions</c>
    /// para que, si en el futuro <see cref="SGV.Web.Integration.Auth.AuthSessionFactory"/>
    /// pasara a validar issuer/audience, este token siga siendo aceptado.
    /// </summary>
    public static string BuildAdminRoleJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminFixtureSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "sgv-tests",
            audience: "sgv-web",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "admin-test"),
                new Claim(ClaimTypes.NameIdentifier, "admin-test"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, RolesSgv.Administrador)
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}