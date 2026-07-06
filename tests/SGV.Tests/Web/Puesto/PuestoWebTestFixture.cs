using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Tests.Web.Cargo;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Shared xUnit fixture (<see cref="IClassFixture{TFixture}"/>) for the Puesto
/// web tests. Encapsula el setup recurrente: una <see cref="SgvWebApplicationFactory"/>
/// base, un <see cref="HttpClient"/> autenticado cableado con un
/// <see cref="FakePuestosApiClient"/> y builders de datos de siembra.
/// Espejo de <c>CargoWebTestFixture</c>.
/// </summary>
public sealed class PuestoWebTestFixture : IDisposable
{
    private readonly SgvWebApplicationFactory _baseFactory;

    public PuestoWebTestFixture()
    {
        _baseFactory = new SgvWebApplicationFactory();
    }

    /// <summary>Factory base sin overrides.</summary>
    public SgvWebApplicationFactory BaseFactory => _baseFactory;

    /// <summary>Seeds Guid estáticos usados por los tests de páginas (PR 2/3).</summary>
    public static readonly Guid SampleUnidadOrganizativaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SampleCargoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SamplePuestoSuperiorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>Devuelve un factory con <see cref="IPuestosApiClient"/> reemplazado por <paramref name="fake"/>.</summary>
    public SgvWebApplicationFactory WithPuestosApiClient(FakePuestosApiClient fake)
        => _baseFactory.WithOverrides(puestosApiClient: fake);

    /// <summary>Devuelve un factory con <see cref="ICargoApiClient"/> reemplazado por <paramref name="fake"/>.</summary>
    public SgvWebApplicationFactory WithCargoApiClient(ICargoApiClient fake)
        => _baseFactory.WithOverrides(cargoApiClient: fake);

    /// <summary>Devuelve un factory con <see cref="IUnidadOrganizativaApiClient"/> reemplazado por <paramref name="fake"/>.</summary>
    public SgvWebApplicationFactory WithUnidadOrganizativaApiClient(IUnidadOrganizativaApiClient fake)
        => _baseFactory.WithOverrides(unidadOrganizativaApiClient: fake);

    /// <summary>
    /// Devuelve un factory con los tres clientes de catálogo (unidades,
    /// cargos, puestos) reemplazados por los fakes provistos. Usado por
    /// los tests de la página Create de Puestos (PR 3A), que carga los
    /// tres catálogos en paralelo vía <c>Task.WhenAll</c>.
    /// </summary>
    public SgvWebApplicationFactory WithCatalogFakes(
        IUnidadOrganizativaApiClient unidadFake,
        ICargoApiClient cargoFake,
        FakePuestosApiClient puestosFake)
        => _baseFactory.WithOverrides(
            unidadOrganizativaApiClient: unidadFake,
            cargoApiClient: cargoFake,
            puestosApiClient: puestosFake);

    /// <summary>Construye un <see cref="PuestoDto"/> con ids aleatorios, útil cuando el test sólo se fija en el shape.</summary>
    public static PuestoDto BuildPuestoDto(
        string codigo,
        string nombre,
        string? descripcion = null,
        Guid? puestoSuperiorId = null)
        => new(
            Guid.NewGuid(),
            codigo,
            nombre,
            descripcion,
            SampleUnidadOrganizativaId,
            "Ventas",
            SampleCargoId,
            "Vendedor",
            puestoSuperiorId);

    /// <summary>
    /// Devuelve un <see cref="HttpClient"/> autenticado cuyo
    /// <see cref="IPuestosApiClient"/> resuelve a <paramref name="apiClient"/>.
    /// La API de auth se stubea para devolver un bearer token fijo.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(FakePuestosApiClient apiClient)
        => await CreateAuthenticatedClientAsync(
            new FakeUnidadOrganizativaApiClient(),
            new FakeCargoApiClient(),
            apiClient);

    /// <summary>
    /// Variante sobrecargada que inyecta los tres fakes de catálogo en el
    /// contenedor. La página Create de Puestos (PR 3A) carga los catálogos
    /// de unidades, cargos y puestos en paralelo vía <c>Task.WhenAll</c>;
    /// los tests que sólo ejercitan el render necesitan los tres overrides
    /// activos (incluso con listas vacías) para evitar fugas al API real.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        IUnidadOrganizativaApiClient unidadFake,
        ICargoApiClient cargoFake,
        FakePuestosApiClient puestosFake)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = _baseFactory.WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            unidadOrganizativaApiClient: unidadFake,
            cargoApiClient: cargoFake,
            puestosApiClient: puestosFake);

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

    /// <summary>Extrae el token antiforgery de un <c>__RequestVerificationToken</c> oculto.</summary>
    public static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    public void Dispose() => _baseFactory?.Dispose();

    /// <summary>
    /// <see cref="HttpMessageHandler"/> mínimo que siempre devuelve una
    /// respuesta preconfigurada. Se usa para stubear el endpoint de auth de
    /// SGV.Api durante los tests.
    /// </summary>
    public sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
