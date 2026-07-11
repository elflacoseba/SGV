using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;

namespace SGV.Tests.Web.Common;

/// <summary>
/// Helper compartido por los <see cref="IClassFixture{TFixture}"/> web que
/// necesitan tokens JWT válidos durante tests de integración.
/// </summary>
public static class AdminJwtTestHelper
{
    /// <summary>
    /// Test key aligned with the development placeholder configured by SGV.Web.
    /// </summary>
    public const string SigningKey = "DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000";

    public const string Issuer = "SGV";

    public const string Audience = "SGV";

    public const string ForeignSigningKey = "FOREIGN-TEST-KEY-DO-NOT-USE-IN-PROD-0000000000000000";

    /// <summary>
    /// Builds a JWT signed with HMAC-SHA256 without an administrator role.
    /// </summary>
    public static string BuildUserJwt(
        string signingKey = SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null)
        => BuildJwt(includeAdminRole: false, signingKey, issuer, audience, expires);

    /// <summary>
    /// Builds a JWT signed with HMAC-SHA256 that includes the
    /// <see cref="ClaimTypes.Role"/> con valor <see cref="RolesSgv.Administrador"/>.
    /// </summary>
    public static string BuildAdminRoleJwt(
        string signingKey = SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null)
        => BuildJwt(includeAdminRole: true, signingKey, issuer, audience, expires);

    private static string BuildJwt(
        bool includeAdminRole,
        string signingKey,
        string issuer,
        string audience,
        DateTime? expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "admin-test"),
            new(ClaimTypes.NameIdentifier, "admin-test"),
            new(ClaimTypes.Name, "admin")
        };

        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, RolesSgv.Administrador));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
