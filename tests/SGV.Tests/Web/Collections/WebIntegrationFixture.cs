using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Common;
using SGV.Tests.Web.Habilidad;
using SGV.Tests.Web.Puesto;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Fixture raíz de la suite web. Posee una única <see cref="SgvWebApplicationFactory"/>
/// base y expone los 6 helpers <c>Task&lt;WebClientLease&gt;</c> firmados según
/// design.md §"Firmas explícitas del composite". Cada helper deriva una factory
/// que pertenece exclusivamente al lease; el fixture sólo libera la root al
/// cierre de la colección.
/// </summary>
public sealed class WebIntegrationFixture : IAsyncLifetime
{
    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };

    private readonly SgvWebApplicationFactory _root;

    public WebIntegrationFixture() => _root = new SgvWebApplicationFactory();

    /// <summary>Acceso tipado a la root. Sólo identidad (no Server/Services).</summary>
    public SgvWebApplicationFactory RootFactory => _root;

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _root.DisposeAsync();

    public Task<WebClientLease> CreateCargoLeaseAsync(
        FakeCargoApiClient cargo, FakeHabilidadApiClient? habilidad = null, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            cargoApiClient: cargo,
            habilidadApiClient: habilidad ?? new FakeHabilidadApiClient()));

    public Task<WebClientLease> CreatePuestoLeaseAsync(
        FakePuestosApiClient puestos,
        IUnidadOrganizativaApiClient? unidad = null,
        ICargoApiClient? cargo = null,
        bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            unidadOrganizativaApiClient: unidad ?? new FakeUnidadOrganizativaApiClient(),
            cargoApiClient: cargo ?? new FakeCargoApiClient(),
            puestosApiClient: puestos));

    public Task<WebClientLease> CreateHabilidadLeaseAsync(
        FakeHabilidadApiClient habilidad, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            habilidadApiClient: habilidad));

    public Task<WebClientLease> CreateUnidadOrganizativaLeaseAsync(
        FakeUnidadOrganizativaApiClient unidad, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            unidadOrganizativaApiClient: unidad));

    /// <summary>Lease sin autenticar con una factory derivada y de propiedad exclusiva.</summary>
    public Task<WebClientLease> CreateAnonymousLeaseAsync()
        => CreateLeaseAsync(f => f.WithOverrides());

    public Task<WebClientLease> CreateAuthOnlyLeaseAsync(bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole)));

    private static void ConfigureBaseUrl(IServiceCollection services)
        => services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");

    private static WebTestBuilders.RecordingHttpMessageHandler BuildAuthHandler(bool adminRole)
    {
        var token = adminRole ? AdminJwtTestHelper.BuildAdminRoleJwt() : AdminJwtTestHelper.BuildUserJwt();
        return new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(token, DateTimeOffset.UtcNow.AddHours(1)))
            });
    }

    private Task<WebClientLease> CreateLeaseAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory)
    {
        var factory = configureFactory(_root);
        var client = factory.CreateClient(ClientOptions);
        return Task.FromResult(new WebClientLease(factory, client, new TestSentinel()));
    }

    private async Task<WebClientLease> CreateAuthenticatedLeaseAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory)
    {
        var factory = configureFactory(_root);
        var client = factory.CreateClient(ClientOptions);

        // La lease se devuelve siempre: si la autenticación falla (pre-existing:
        // el endpoint devuelve 200 OK en vez de 302 Found en develop), el cliente
        // queda sin cookie de auth pero sigue siendo útil. La validación de auth
        // es per-test, no del composite infra.
        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInResponse);

        _ = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        return new WebClientLease(factory, client, new TestSentinel());
    }
}