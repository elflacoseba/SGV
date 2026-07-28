using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web._Shared;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Common;
using SGV.Tests.Web.Habilidad;
using SGV.Tests.Web.Puesto;
using SGV.Tests.Web.Usuario;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Usuarios;
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
    [ModuleInitializer]
    internal static void ConfigureTestFileWatcher()
        => Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");

    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };

    private readonly SgvWebApplicationFactory _root;
    private int _disposed;

    public WebIntegrationFixture() => _root = new SgvWebApplicationFactory();

    /// <summary>Acceso tipado a la root. Sólo identidad (no Server/Services).</summary>
    public SgvWebApplicationFactory RootFactory => _root;

    public Task InitializeAsync() => Task.CompletedTask;
    
    public async Task DisposeAsync()
    {
        // Idempotencia: una segunda llamada (p. ej. IAsyncLifetime que lo
        // invoca más de una vez) NO debe volver a disponer la root factory.
        // Sin esta guarda, la segunda llamada puede causar problemas.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Disponer la root factory al cerrar la colección. No se usa el lock
        // global porque el dispose del fixture solo se ejecuta cuando toda la
        // colección termina (no hay tests concurrentes creando leases).
        await _root.DisposeAsync();
    }

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

    /// <summary>
    /// Lease autenticado contra el módulo Ocupaciones. Toma un
    /// <see cref="FakeOcupacionApiClient"/> y la inyecta en el contenedor
    /// del host vía <see cref="SgvWebApplicationFactory.WithOcupacionApiClient"/>.
    /// Agregado en Slice 2 del change <c>2026-07-28-web-ocupaciones-issue-208</c>
    /// (#208); sigue la firma estándar de los otros módulos
    /// (<see cref="CreatePuestoLeaseAsync"/>, etc.).
    /// </summary>
    public Task<WebClientLease> CreateOcupacionLeaseAsync(
        IOcupacionApiClient ocupacion, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            ocupacionApiClient: ocupacion));

    public Task<WebClientLease> CreateHabilidadLeaseAsync(
        FakeHabilidadApiClient habilidad, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            habilidadApiClient: habilidad));

    /// <summary>
    /// Lease autenticado contra el módulo Personas. Toma una
    /// <see cref="FakePersonaApiClient"/> (helper interno del suite de tests
    /// web de Personas) y la inyecta en el contenedor del host vía
    /// <see cref="SgvWebApplicationFactory.WithPersonaApiClient"/>.
    /// Agregado en PR 4/4 del change
    /// <c>2026-07-14-frontend-crud-personas</c>; sigue la firma estándar de
    /// los otros módulos (<see cref="CreateCargoLeaseAsync"/>, etc.).
    /// </summary>
    public Task<WebClientLease> CreatePersonaLeaseAsync(
        IPersonaApiClient persona,
        IHabilidadApiClient? habilidad = null,
        bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            personaApiClient: persona,
            habilidadApiClient: habilidad ?? new FakeHabilidadApiClient()));

    /// <summary>
    /// Lease autenticado contra el módulo Usuarios. Toma un
    /// <see cref="FakeUsuarioApiClient"/> y la inyecta en el contenedor
    /// del host vía
    /// <see cref="SgvWebApplicationFactory.WithUsuarioApiClient"/>.
    /// Agregado en PR 2/4 del change <c>Implementa módulo usuarios</c>;
    /// sigue la firma estándar de los otros módulos
    /// (<see cref="CreateCargoLeaseAsync"/>, etc.).
    /// </summary>
    public Task<WebClientLease> CreateUsuarioLeaseAsync(
        IUsuarioApiClient usuario, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            usuarioApiClient: usuario));

    public Task<WebClientLease> CreateUsuarioLeaseAsync(
        IUsuarioApiClient usuario,
        IPersonaApiClient personaApiClient,
        bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            personaApiClient: personaApiClient,
            usuarioApiClient: usuario));

    /// <summary>
    /// Overload que adjunta un <see cref="RecordingLoggerProvider"/> al
    /// pipeline de logging del host. Útil para tests que necesitan
    /// assertear entradas de log + scope estructurado emitidos por el
    /// código bajo prueba (e.g. issue #164: BFF que loggea fallos
    /// upstream con scope conteniendo <c>Search</c>/<c>Sort</c>/<c>Segmento</c>/<c>CorrelationId</c>).
    /// El provider es compartido por referencia con el test, así que las
    /// entradas capturadas están disponibles inmediatamente después de
    /// ejecutar la request HTTP.
    /// </summary>
    public Task<WebClientLease> CreateUsuarioLeaseAsync(
        IUsuarioApiClient usuario,
        IPersonaApiClient personaApiClient,
        RecordingLoggerProvider recordingLoggerProvider,
        bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            personaApiClient: personaApiClient,
            usuarioApiClient: usuario,
            recordingLoggerProvider: recordingLoggerProvider));

    public Task<WebClientLease> CreateUnidadOrganizativaLeaseAsync(
        FakeUnidadOrganizativaApiClient unidad, bool adminRole = false)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl, BuildAuthHandler(adminRole),
            unidadOrganizativaApiClient: unidad));

    /// <summary>
    /// Lease anónimo para tests del módulo de setup inicial (issue #195
    /// / WU-4). No requiere autenticación porque el flujo de setup es
    /// la primera acción del sistema cuando <c>AspNetUsers</c> está
    /// vacía. Toma un fake de <see cref="SGV.Web.Integration.Setup.ISetupApiClient"/>
    /// para controlar el status, el catálogo de <c>TipoDocumento</c> y
    /// el resultado de <c>POST /auth/setup</c> sin necesidad de la API
    /// real ni de MySQL.
    /// </summary>
    public Task<WebClientLease> CreateSetupLeaseAsync(
        SGV.Web.Integration.Setup.ISetupApiClient setupApiClient)
        => CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(
                ConfigureBaseUrl,
                setupApiClient: setupApiClient),
            NoOpBootstrapAsync);

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

    /// <summary>
    /// Espejo de <see cref="CreateCargoBridgeLeaseAsync"/> para el módulo
    /// Personas. Agregado en Slice 3b del change
    /// <c>implementa-persona-habilidades</c> para soportar el test
    /// end-to-end del bridge JWT contra el subrecurso
    /// <c>persona-skill</c> (<c>PersonaHabilidadesIntegrationTests.Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi</c>).
    /// </summary>
    public Task<WebClientLease> CreatePersonaBridgeLeaseAsync(
        HttpMessageHandler authApiHandler,
        HttpMessageHandler personaApiHandler)
        => CreateAuthenticatedLeaseAsync(f => f.WithOverrides(
            ConfigureBaseUrl,
            authApiHandler,
            personaApiHandler: personaApiHandler));

    private static void ConfigureBaseUrl(IServiceCollection services)
    {
        services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
        services.Configure<JwtOptions>(o =>
        {
            o.SigningKey = AdminJwtTestHelper.SigningKey;
            o.Issuer = AdminJwtTestHelper.Issuer;
            o.Audience = AdminJwtTestHelper.Audience;
        });
    }

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
        SgvWebApplicationFactory factory;
        HttpClient client;

        try
        {
            factory = configureFactory(_root);
            client = factory.CreateClient(ClientOptions);
            captureClient?.Invoke(client);
            captureFactory?.Invoke(factory);
        }
        catch
        {
            // If we never got to assign these, the catch below handles cleanup.
            throw;
        }

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