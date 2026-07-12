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
        => CreateLeaseWithBootstrapAsync(f => f.WithOverrides(), NoOpBootstrapAsync);

    public Task<WebClientLease> CreateAuthOnlyLeaseAsync(bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole)));

    /// <summary>
    /// Lease autenticado contra el bridge web→API con un <see cref="HttpMessageHandler"/>
    /// de cargo API configurable. Variante narrow introducida en PR 2b-4 para
    /// eliminar el <c>using var factory = new SgvWebApplicationFactory().WithOverrides(...)</c>
    /// anónimo de <c>ApiBearerTokenIntegrationTests</c>: ese test intercambia el
    /// <c>PrimaryHandler</c> del typed-client de cargo para grabar las requests
    /// salientes y verificar la propagación del bearer token. La firma estándar
    /// <see cref="CreateCargoLeaseAsync"/> no cubre el override de handler (sólo
    /// expone el fake tipado), por eso se requiere esta segunda entrada — única
    /// adición de API en este lote, justificada por el conteo de 33 sitios sin
    /// <c>using</c> en design.md §"Inventario source-backed (rg)" + la
    /// imposibilidad de encajar el test sin una factory derivada sin dispose.
    /// </summary>
    public Task<WebClientLease> CreateCargoBridgeLeaseAsync(
        HttpMessageHandler authApiHandler,
        HttpMessageHandler cargoApiHandler)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl,
            authApiHandler,
            cargoApiHandler: cargoApiHandler));

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

    /// <summary>
    /// Crea una factory derivada y un <see cref="HttpClient"/> desde la raíz
    /// compartida, ejecuta el callback de bootstrap y sólo construye el
    /// <see cref="WebClientLease"/> al final. Si el callback tira, libera los
    /// recursos en orden <c>client → factory</c> (mismo orden que
    /// <see cref="WebClientLease.DisposeAsync"/> sin el paso del sentinel,
    /// porque éste aún no fue construido) y vuelve a lanzar la excepción
    /// original. La raíz compartida del fixture NO se ve afectada.
    /// Diseñado para ser el punto único de cleanup del composite infra;
    /// todos los helpers públicos delegan acá.
    /// </summary>
    internal Task<WebClientLease> CreateLeaseWithBootstrapAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory,
        Func<HttpClient, Task> bootstrap)
        => CreateLeaseWithBootstrapAsync(configureFactory, bootstrap, captureFactory: null, captureClient: null);

    /// <summary>
    /// Overload de testing que acepta un <see cref="HttpClient"/> pre-construido
    /// (típicamente un wrapper que tira en Dispose para simular fallas de
    /// cleanup). Igual contrato de cleanup que el overload estándar: si el
    /// callback tira, se dispone el cliente y la factory en orden
    /// <c>client → factory</c>, suprimiendo cualquier excepción del dispose
    /// para preservar la excepción original. La factory se construye vía el
    /// callback <paramref name="configureFactory"/> para mantener la raíz
    /// compartida intacta.
    /// </summary>
    internal async Task<WebClientLease> CreateLeaseWithBootstrapAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory,
        HttpClient client,
        Func<HttpClient, Task> bootstrap)
    {
        var factory = configureFactory(_root);

        try
        {
            await bootstrap(client);
        }
        catch
        {
            TryDisposeClient(client);
            await TryDisposeFactoryAsync(factory);
            throw;
        }

        return new WebClientLease(factory, client, new TestSentinel());
    }

    /// <summary>
    /// Overload canónico de testing. Construye la factory derivada y el
    /// <see cref="HttpClient"/> desde la raíz compartida, ejecuta el
    /// callback de bootstrap, y sólo construye el <see cref="WebClientLease"/>
    /// al final. Si el callback tira, libera los recursos en orden
    /// <c>client → factory</c> (mismo orden que
    /// <see cref="WebClientLease.DisposeAsync"/> sin el paso del sentinel,
    /// porque éste aún no fue construido) y vuelve a lanzar la excepción
    /// original. La raíz compartida del fixture NO se ve afectada.
    ///
    /// PR 2b-4 review #995: el <c>factory.CreateClient</c> se ejecuta
    /// DENTRO del try (antes estaba antes y un fallo del cliente
    /// dejaba la factory perdida). El cleanup de cada recurso corre en
    /// su propio try/catch anidado para que una falla del dispose NO
    /// reemplace la excepción original del bootstrap.
    ///
    /// Los callbacks <paramref name="captureFactory"/> y
    /// <paramref name="captureClient"/> son el único canal de
    /// observación post-dispose que no depende de contadores estáticos
    /// compartidos (inmunes a paralelismo inter-colección): el test
    /// retiene la referencia al cliente derivado y verifica
    /// posteriormente vía <c>HttpClient.GetAsync</c> que el dispose fue
    /// efectivamente llamado (post-dispose lanza
    /// <see cref="ObjectDisposedException"/>).
    /// </summary>
    internal async Task<WebClientLease> CreateLeaseWithBootstrapAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory,
        Func<HttpClient, Task> bootstrap,
        Action<SgvWebApplicationFactory>? captureFactory,
        Action<HttpClient>? captureClient)
    {
        var factory = configureFactory(_root);
        HttpClient? client = null;

        try
        {
            client = factory.CreateClient(ClientOptions);
            captureClient?.Invoke(client);
            captureFactory?.Invoke(factory);
            await bootstrap(client);
        }
        catch
        {
            if (client is not null)
            {
                TryDisposeClient(client);
            }

            await TryDisposeFactoryAsync(factory);

            throw;
        }

        return new WebClientLease(factory, client, new TestSentinel());
    }

    private static void TryDisposeClient(HttpClient client)
    {
        try
        {
            client.Dispose();
        }
        catch
        {
            // Suprimido: la falla del dispose no debe reemplazar la
            // excepción original del bootstrap.
        }
    }

    private static async Task TryDisposeFactoryAsync(SgvWebApplicationFactory factory)
    {
        try
        {
            await factory.DisposeAsync();
        }
        catch
        {
            // Suprimido: idem.
        }
    }

    private static Task NoOpBootstrapAsync(HttpClient client) => Task.CompletedTask;

    private Task<WebClientLease> CreateAuthenticatedLeaseAsync(
        Func<SgvWebApplicationFactory, SgvWebApplicationFactory> configureFactory)
        => CreateLeaseWithBootstrapAsync(configureFactory, AuthenticateClientAsync);

    /// <summary>
    /// Bootstrap estándar: GET al sign-in, extracción del token antiforgery,
    /// POST con credenciales. La lease se devuelve siempre: si la
    /// autenticación falla (pre-existing: el endpoint devuelve 200 OK en
    /// vez de 302 Found en develop), el cliente queda sin cookie de auth
    /// pero sigue siendo útil. La validación de auth es per-test, no del
    /// composite infra.
    /// </summary>
    internal static async Task AuthenticateClientAsync(HttpClient client)
    {
        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInResponse);

        _ = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));
    }
}