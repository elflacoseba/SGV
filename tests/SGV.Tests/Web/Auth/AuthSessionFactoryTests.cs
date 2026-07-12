using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

    /// <summary>
    /// Construye la fábrica con las opciones del test envueltas en
    /// <see cref="IOptions{TOptions}"/>. La signatura de la nueva
    /// <see cref="AuthSessionFactory"/> (issue #121) recibe el logger y las
    /// opciones por DI; este helper mantiene los tests existentes al nivel
    /// unitario, sin levantar un host completo.
    /// </summary>
    private static AuthSessionFactory CreateFactory(JwtOptions? options = null) =>
        new(NullLogger<AuthSessionFactory>.Instance, Microsoft.Extensions.Options.Options.Create(options ?? Options));

    [Fact]
    public void CreatePrincipal_WithValidToken_AddsRoleAndTokenClaims()
    {
        var response = new LoginResponse(AdminJwtTestHelper.BuildAdminRoleJwt(), DateTimeOffset.UtcNow.AddHours(1));

        var principal = CreateFactory().CreatePrincipal(Request, response);

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
        {
            CreateFactory().CreatePrincipal(Request, response);
        });
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
        {
            CreateFactory().CreatePrincipal(Request, response);
        });
    }

    // Los casos marcados como "plano" disparan ArgumentException (raíz, no
    // SecurityTokenArgumentException) porque el Base64Url decoder del handler
    // falla antes de que el validador pueda clasificar el error. Cualquier
    // cambio de Microsoft.IdentityModel.Tokens que reclasifique estos casos
    // sigue siendo detectado por Assert.ThrowsAny<ArgumentException> porque
    // SecurityTokenMalformedException : SecurityTokenArgumentException : ArgumentException.
    // El test unitario se complementa con el test de integración
    // Post_SignIn_WhenApiReturnsInvalidToken_ShowsAuthenticationErrorWithoutCookie
    // que verifica HTTP 200 (no 500) para el caso "token-123".
    public static IEnumerable<object[]> MalformedTokenCases()
    {
        // SecurityTokenMalformedException: sin puntos separadores, segmentos
        // no-JWT detectados por el handler antes del decoder.
        yield return ["token-123"];
        yield return ["single-segment"];
        yield return ["only.two"];
        yield return ["%%%.%%%.%%%"];
        // ArgumentException plano: segmentos que el Base64Url decoder no
        // puede procesar, antes de que el handler intervenga.
        yield return ["a.b.c.d.e"];
        yield return ["abc.def.ghi"];
        yield return ["a.b."];
    }
}