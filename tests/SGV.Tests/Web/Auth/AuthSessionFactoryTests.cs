using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

public sealed class AuthSessionFactoryTests
{
    private static readonly LoginRequest Request = new("admin", "Password1!");

    private static readonly JwtOptions Options = new()
    {
        Issuer = AdminJwtTestHelper.Issuer,
        Audience = AdminJwtTestHelper.Audience,
        SigningKey = AdminJwtTestHelper.SigningKey
    };

    [Fact]
    public void CreatePrincipal_WithValidToken_AddsRoleAndTokenClaims()
    {
        var response = new LoginResponse(AdminJwtTestHelper.BuildAdminRoleJwt(), DateTimeOffset.UtcNow.AddHours(1));

        var principal = AuthSessionFactory.CreatePrincipal(NullLogger.Instance, Options, Request, response);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.True(principal.IsInRole(RolesSgv.Administrador));
        Assert.Contains(principal.Claims, claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier && claim.Value == "admin-test");
    }

    [Theory]
    [MemberData(nameof(InvalidTokenCases))]
    public void CreatePrincipal_WithInvalidToken_RejectsToken(string token)
    {
        var response = new LoginResponse(token, DateTimeOffset.UtcNow.AddHours(1));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            AuthSessionFactory.CreatePrincipal(NullLogger.Instance, Options, Request, response));
    }

    public static IEnumerable<object[]> InvalidTokenCases()
    {
        yield return [AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: AdminJwtTestHelper.InvalidSigningKey)];
        yield return [AdminJwtTestHelper.BuildAdminRoleJwt(expires: DateTime.UtcNow.AddMinutes(-1))];
        yield return [AdminJwtTestHelper.BuildAdminRoleJwt(issuer: "WrongIssuer")];
        yield return [AdminJwtTestHelper.BuildAdminRoleJwt(audience: "WrongAudience")];
    }
}
