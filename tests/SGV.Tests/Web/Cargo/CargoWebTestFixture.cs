using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Tests.Web.Habilidad;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Shared xUnit fixture (<see cref="IClassFixture{TFixture}"/>) for the
/// Cargo web tests. Encapsulates the recurring setup: a base
/// <see cref="SgvWebApplicationFactory"/>, an authenticated
/// <see cref="HttpClient"/> wired with a <see cref="FakeCargoApiClient"/>,
/// and a small set of seed-data builders (<see cref="BuildCargoDto"/>).
/// Test names and assertions in the consuming classes are not affected;
/// only the duplicated helpers move here.
/// </summary>
public sealed class CargoWebTestFixture : IDisposable
{
    private readonly SgvWebApplicationFactory _baseFactory;

    public CargoWebTestFixture()
    {
        _baseFactory = new SgvWebApplicationFactory();
    }

    /// <summary>
    /// Base factory without overrides. Tests that need no cargo override
    /// can call <c>BaseFactory.CreateClient(...)</c> directly.
    /// </summary>
    public SgvWebApplicationFactory BaseFactory => _baseFactory;

    /// <summary>
    /// Returns a new factory with <see cref="ICargoApiClient"/> swapped
    /// for the supplied <paramref name="fake"/>. The base factory is
    /// left untouched so multiple tests can run with different fakes.
    /// </summary>
    public SgvWebApplicationFactory WithCargoApiClient(FakeCargoApiClient fake)
        => _baseFactory.WithOverrides(cargoApiClient: fake);

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid JuniorNivelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seeds the fake catalog ids used by the Create page tests.
    /// </summary>
    public static readonly Guid SeniorNivelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Builds a fresh <see cref="CargoDto"/> with random id and nivel id,
    /// useful when a test only cares about the data shape.
    /// </summary>
    public static CargoDto BuildCargoDto(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    /// <summary>
    /// Returns an authenticated <see cref="HttpClient"/> whose
    /// <see cref="ICargoApiClient"/> resolves to <paramref name="apiClient"/>.
    /// The auth API is stubbed to return a fixed bearer token.
    /// Mantiene la firma pre-existente: el usuario autenticado NO tiene
    /// claims de rol, por lo que <c>User.IsInRole(RolesSgv.Administrador)</c>
    /// devuelve <c>false</c> en tests que la usen para chequeos explícitos.
    /// </summary>
    public Task<HttpClient> CreateAuthenticatedClientAsync(FakeCargoApiClient apiClient)
        => CreateAuthenticatedClientAsync(apiClient, new FakeHabilidadApiClient(), adminRole: false);

    /// <summary>
    /// Variante sobrecargada que también inyecta un
    /// <see cref="FakeHabilidadApiClient"/> en el contenedor y permite
    /// optar por autenticar con rol <see cref="RolesSgv.Administrador"/>.
    /// El "admin" se modela firmando un JWT con <c>ClaimTypes.Role</c>:
    /// <see cref="AuthSessionFactory.TryAddTokenClaims"/> lo lee y lo
    /// agrega a la identidad de la cookie, así <c>User.IsInRole(...)</c>
    /// devuelve <c>true</c> dentro del pipeline de Razor Pages.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        FakeCargoApiClient apiClient,
        FakeHabilidadApiClient habilidadApiClient,
        bool adminRole)
    {
        var accessToken = adminRole ? BuildAdminRoleJwt() : "token-123";

        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(accessToken, DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = _baseFactory.WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            cargoApiClient: apiClient,
            habilidadApiClient: habilidadApiClient);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(signInResponse);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    /// <summary>
    /// Genera un JWT firmado con un HMAC dummy que incluye el claim
    /// <see cref="ClaimTypes.Role"/> con valor <see cref="RolesSgv.Administrador"/>.
    /// No usamos la clave real de <c>JwtOptions</c> porque
    /// <see cref="AuthSessionFactory.TryAddTokenClaims"/> NO valida la
    /// firma — sólo lee los claims. El HMAC es suficiente para que
    /// <c>JwtSecurityTokenHandler.WriteToken</c> produzca un token con
    /// la estructura canónica (header.payload.signature).
    /// </summary>
    private static string BuildAdminRoleJwt()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("sgv-tests-fixture-admin-jwt-signing-key-32bytes-long-enough"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "sgv-tests",
            audience: "sgv-web",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "admin-test"),
                new Claim(ClaimTypes.NameIdentifier, "admin-test"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, RolesSgv.Administrador)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Extracts the antiforgery token rendered in a <c>__RequestVerificationToken</c>
    /// hidden input. Fails the test if the token is not present.
    /// </summary>
    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    public void Dispose() => _baseFactory?.Dispose();

    /// <summary>
    /// Minimal <see cref="HttpMessageHandler"/> that always returns a
    /// preconfigured <see cref="HttpResponseMessage"/>. Used to stub the
    /// SGV.Api auth endpoint during tests.
    /// </summary>
    public sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}