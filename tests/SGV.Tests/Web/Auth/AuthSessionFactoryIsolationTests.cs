using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Aislamiento de validación JWT entre hosts SGV.Web. Escenario del spec
/// "Aislamiento de validación de sesión web" (issue #121): cada host debe
/// validar únicamente tokens firmados con su propia <c>Jwt:SigningKey</c>.
/// Antes del refactor de #121, una caché estática de
/// <c>TokenValidationParameters</c> hacía que el primer host inicializara la
/// configuración y todos los hosts posteriores la heredaran. Esta batería de
/// tests construye dos hosts independientes con claves distintas, firma un
/// token con cada clave y verifica que cada host acepta sólo el suyo.
///
/// Las dos clases de escenarios del spec son:
/// <list type="number">
///   <item>"Hosts con opciones JWT distintas validan solo su propia
///   configuración" — cada host acepta su token, rechaza el del otro.</item>
///   <item>"Validaciones repetidas permanecen independientes" — llamar a la
///   validación dos veces en hosts distintos no contamina al siguiente.</item>
/// </list>
/// </summary>
public sealed class AuthSessionFactoryIsolationTests
{
    // Dos claves >= 32 bytes UTF-8, distintas entre sí, que satisfacen el
    // validator de Program.cs (>=32 bytes) y sirven como fixtures opuestos.
    private const string KeyA = "ISOLATION-TEST-KEY-A-0000000000000000000000000";
    private const string KeyB = "ISOLATION-TEST-KEY-B-0000000000000000000000000";

    private const string Issuer = "SGV";
    private const string Audience = "SGV";
    private const string WebBaseUrl = "https://api.example.com";

    [Fact]
    public async Task HostA_AcceptsOwnToken_RejectsHostBToken()
    {
        // Arrange — Host A configurado con KeyA, Host B con KeyB.
        var tokenA = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyA, issuer: Issuer, audience: Audience);
        var tokenB = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyB, issuer: Issuer, audience: Audience);

        await using var hostA = BuildHost(KeyA);
        await using var hostB = BuildHost(KeyB);

        var factoryA = hostA.Services.GetRequiredService<IAuthSessionFactory>();
        var factoryB = hostB.Services.GetRequiredService<IAuthSessionFactory>();

        var responseA = new LoginResponse(tokenA, DateTimeOffset.UtcNow.AddHours(1));
        var responseB = new LoginResponse(tokenB, DateTimeOffset.UtcNow.AddHours(1));

        // Act + Assert — Host A valida su propio token.
        var principalA = factoryA.CreatePrincipal(LoginRequest(), responseA);
        Assert.True(principalA.Identity?.IsAuthenticated);

        // Act + Assert — Host A rechaza el token firmado con KeyB.
        var exA = Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            factoryA.CreatePrincipal(LoginRequest(), responseB);
        });
        Assert.NotNull(exA);
    }

    [Fact]
    public async Task HostB_AcceptsOwnToken_RejectsHostAToken()
    {
        // Arrange — Host B con KeyB, token A firmado con KeyA.
        var tokenA = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyA, issuer: Issuer, audience: Audience);
        var tokenB = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyB, issuer: Issuer, audience: Audience);

        await using var hostA = BuildHost(KeyA);
        await using var hostB = BuildHost(KeyB);

        var factoryA = hostA.Services.GetRequiredService<IAuthSessionFactory>();
        var factoryB = hostB.Services.GetRequiredService<IAuthSessionFactory>();

        var responseA = new LoginResponse(tokenA, DateTimeOffset.UtcNow.AddHours(1));
        var responseB = new LoginResponse(tokenB, DateTimeOffset.UtcNow.AddHours(1));

        // Act + Assert — Host B valida su propio token.
        var principalB = factoryB.CreatePrincipal(LoginRequest(), responseB);
        Assert.True(principalB.Identity?.IsAuthenticated);

        // Act + Assert — Host B rechaza el token firmado con KeyA.
        var exB = Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            factoryB.CreatePrincipal(LoginRequest(), responseA);
        });
        Assert.NotNull(exB);
    }

    [Fact]
    public async Task SecondInvocation_DoesNotObserveResidualParameters()
    {
        // Escenario "Validaciones repetidas independientes" del spec:
        // dos invocaciones consecutivas no deben contaminarse.
        var tokenA = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyA, issuer: Issuer, audience: Audience);
        var tokenB = AdminJwtTestHelper.BuildAdminRoleJwt(signingKey: KeyB, issuer: Issuer, audience: Audience);

        await using var hostA = BuildHost(KeyA);
        await using var hostB = BuildHost(KeyB);

        var factoryA = hostA.Services.GetRequiredService<IAuthSessionFactory>();
        var factoryB = hostB.Services.GetRequiredService<IAuthSessionFactory>();

        // Primera invocación sobre host A con token A: debe pasar.
        var firstA = factoryA.CreatePrincipal(
            LoginRequest(),
            new LoginResponse(tokenA, DateTimeOffset.UtcNow.AddHours(1)));
        Assert.True(firstA.Identity?.IsAuthenticated);

        // Segunda invocación sobre host B con token B: no debe quedar rastro
        // residual de los parámetros de host A.
        var secondB = factoryB.CreatePrincipal(
            LoginRequest(),
            new LoginResponse(tokenB, DateTimeOffset.UtcNow.AddHours(1)));
        Assert.True(secondB.Identity?.IsAuthenticated);

        // Y host B con token A sigue rechazando (sin "recordar" el token A
        // que pasó por host A en la primera invocación).
        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            factoryB.CreatePrincipal(
                LoginRequest(),
                new LoginResponse(tokenA, DateTimeOffset.UtcNow.AddHours(1)));
        });
    }

    private static LoginRequest LoginRequest() => new("admin", "Password1!");

    /// <summary>
    /// Construye un host SGV.Web con una <c>Jwt:SigningKey</c> inyectada por
    /// configuración en memoria. Patrón copiado de
    /// <c>tests/SGV.Tests/Seguridad/JwtOptionsTests.cs:32-40</c>: dos anclas
    /// son <c>SgvApi:BaseUrl</c> (para que <c>ValidateOnStart</c> no bloquee
    /// la construcción del host) y <c>Jwt:SigningKey</c> (≥32 bytes UTF-8).
    /// </summary>
    private static WebApplicationFactory<SGV.Web.Program> BuildHost(string signingKey) =>
        new WebApplicationFactory<SGV.Web.Program>()
            .WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["SgvApi:BaseUrl"] = WebBaseUrl,
                        ["Jwt:SigningKey"] = signingKey
                    })));
}