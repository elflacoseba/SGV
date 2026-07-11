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

    // Cobertura específica para tokens que NO son JWT bien formados. En
    // Microsoft.IdentityModel.Tokens 8.x las excepciones de token malformado
    // se reparten entre dos ramas del árbol:
    //   - SecurityTokenArgumentException (subclase de ArgumentException) cuando
    //     el handler detecta "JWT must have three segments" y errores similares.
    //   - ArgumentException plano cuando el Base64Url decode falla antes de que
    //     el handler pueda clasificar el error (e.g. segmentos con bytes no-base64).
    // Sin este test, un access_token corrupto (proxy, baseUrl incorrecto,
    // respuesta no-JSON de un balanceador) se propaga como 500 en lugar de
    // quedar en la página con error recuperable. Assert.ThrowsAny<ArgumentException>
    // cubre las tres variantes (raíz, SecurityTokenArgumentException,
    // SecurityTokenMalformedException).
    [Theory]
    [MemberData(nameof(MalformedTokenCases))]
    public void CreatePrincipal_WithMalformedToken_RejectsToken(string token)
    {
        var response = new LoginResponse(token, DateTimeOffset.UtcNow.AddHours(1));

        Assert.ThrowsAny<ArgumentException>(() =>
            AuthSessionFactory.CreatePrincipal(NullLogger.Instance, Options, Request, response));
    }

    public static IEnumerable<object[]> MalformedTokenCases()
    {
        // Sin segmentos → SecurityTokenMalformedException.
        yield return ["token-123"];
        // Un solo segmento → SecurityTokenMalformedException.
        yield return ["single-segment"];
        // Dos segmentos (necesita tres para JWS) → SecurityTokenMalformedException.
        yield return ["only.two"];
        // Cinco segmentos con bytes no-base64 en la primera parte → ArgumentException
        // plano (Base64Url decode falla antes de que el handler clasifique el error).
        yield return ["a.b.c.d.e"];
        // Segmentos con bytes no base64 → ArgumentException plano.
        yield return ["%%%.%%%.%%%"];
    }
}
