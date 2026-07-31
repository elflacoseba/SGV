using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Setup;
using SGV.Web.Integration.Usuarios;
using SGV.Web.Integration.Vacantes;

namespace SGV.Tests.Web;

/// <summary>
/// WebApplicationFactory for SGV.Web (Razor Pages shell).
/// Supports service overrides and a fake SGV.Api auth handler for integration tests.
/// </summary>
public sealed class SgvWebApplicationFactory : WebApplicationFactory<SGV.Web.Program>
{
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly HttpMessageHandler? _authApiHandler;
    private readonly HttpMessageHandler? _cargoApiHandler;
    private readonly HttpMessageHandler? _personaApiHandler;
    private readonly IUnidadOrganizativaApiClient? _unidadOrganizativaApiClient;
    private readonly ICargoApiClient? _cargoApiClient;
    private readonly IHabilidadApiClient? _habilidadApiClient;
    private readonly IPuestosApiClient? _puestosApiClient;
    private readonly IPersonaApiClient? _personaApiClient;
    private readonly IUsuarioApiClient? _usuarioApiClient;
    private readonly ISetupApiClient? _setupApiClient;
    private readonly IOcupacionApiClient? _ocupacionApiClient;
    private readonly IVacanteApiClient? _vacanteApiClient;
    private readonly RecordingLoggerProvider? _recordingLoggerProvider;

    public SgvWebApplicationFactory()
    {
    }

    private SgvWebApplicationFactory(
        Action<IServiceCollection>? configureServices,
        HttpMessageHandler? authApiHandler,
        HttpMessageHandler? cargoApiHandler,
        HttpMessageHandler? personaApiHandler,
        IUnidadOrganizativaApiClient? unidadOrganizativaApiClient,
        ICargoApiClient? cargoApiClient,
        IHabilidadApiClient? habilidadApiClient,
        IPuestosApiClient? puestosApiClient,
        IPersonaApiClient? personaApiClient,
        IUsuarioApiClient? usuarioApiClient,
        ISetupApiClient? setupApiClient = null,
        IOcupacionApiClient? ocupacionApiClient = null,
        IVacanteApiClient? vacanteApiClient = null,
        RecordingLoggerProvider? recordingLoggerProvider = null)
    {
        _configureServices = configureServices;
        _authApiHandler = authApiHandler;
        _cargoApiHandler = cargoApiHandler;
        _personaApiHandler = personaApiHandler;
        _unidadOrganizativaApiClient = unidadOrganizativaApiClient;
        _cargoApiClient = cargoApiClient;
        _habilidadApiClient = habilidadApiClient;
        _puestosApiClient = puestosApiClient;
        _personaApiClient = personaApiClient;
        _usuarioApiClient = usuarioApiClient;
        _setupApiClient = setupApiClient;
        _ocupacionApiClient = ocupacionApiClient;
        _vacanteApiClient = vacanteApiClient;
        _recordingLoggerProvider = recordingLoggerProvider;
    }

    public SgvWebApplicationFactory WithOverrides(
        Action<IServiceCollection>? configureServices = null,
        HttpMessageHandler? authApiHandler = null,
        HttpMessageHandler? cargoApiHandler = null,
        HttpMessageHandler? personaApiHandler = null,
        IUnidadOrganizativaApiClient? unidadOrganizativaApiClient = null,
        ICargoApiClient? cargoApiClient = null,
        IHabilidadApiClient? habilidadApiClient = null,
        IPuestosApiClient? puestosApiClient = null,
        IPersonaApiClient? personaApiClient = null,
        IUsuarioApiClient? usuarioApiClient = null,
        ISetupApiClient? setupApiClient = null,
        IOcupacionApiClient? ocupacionApiClient = null,
        IVacanteApiClient? vacanteApiClient = null,
        RecordingLoggerProvider? recordingLoggerProvider = null)
    {
        return new SgvWebApplicationFactory(
            configureServices,
            authApiHandler,
            cargoApiHandler,
            personaApiHandler,
            unidadOrganizativaApiClient,
            cargoApiClient,
            habilidadApiClient,
            puestosApiClient,
            personaApiClient,
            usuarioApiClient,
            setupApiClient,
            ocupacionApiClient,
            vacanteApiClient,
            recordingLoggerProvider);
    }

    /// <summary>
    /// Convenience helper to swap <see cref="IPersonaApiClient"/> for a fake
    /// without touching the rest of the configuration surface. Mirror de
    /// <see cref="WithHabilidadApiClient"/> y <see cref="WithPuestosApiClient"/>;
    /// agregado en PR 4/4 del change Personas para que la suite web del módulo
    /// no requiera un backend real.
    /// </summary>
    public SgvWebApplicationFactory WithPersonaApiClient(IPersonaApiClient fake)
        => WithOverrides(personaApiClient: fake);

    /// <summary>
    /// Convenience helper to swap <see cref="IUsuarioApiClient"/> for a fake
    /// without touching the rest of the configuration surface. Mirror de
    /// <see cref="WithPersonaApiClient"/>; agregado en PR 2/4 del change
    /// <c>Implementa módulo usuarios</c> para que la suite web del módulo
    /// no requiera un backend real.
    /// </summary>
    public SgvWebApplicationFactory WithUsuarioApiClient(IUsuarioApiClient fake)
        => WithOverrides(usuarioApiClient: fake);

    /// <summary>
    /// Convenience helper para inyectar un fake de
    /// <see cref="ISetupApiClient"/> en el contenedor del host. El
    /// setup inicial (issue #195 / WU-4) no requiere autenticación,
    /// por eso este helper se usa combinado con
    /// <see cref="WebIntegrationFixture.CreateSetupLeaseAsync"/> en
    /// vez del <c>CreateAuthenticatedLeaseAsync</c> estándar.
    /// </summary>
    public SgvWebApplicationFactory WithSetupApiClient(ISetupApiClient fake)
        => WithOverrides(setupApiClient: fake);

    /// <summary>
    /// Convenience helper to swap <see cref="IHabilidadApiClient"/> for a fake
    /// without touching the rest of the configuration surface.
    /// </summary>
    public SgvWebApplicationFactory WithHabilidadApiClient(IHabilidadApiClient fake)
        => WithOverrides(habilidadApiClient: fake);

    /// <summary>
    /// Convenience helper to swap <see cref="IPuestosApiClient"/> for a fake
    /// without touching the rest of the configuration surface.
    /// </summary>
    public SgvWebApplicationFactory WithPuestosApiClient(IPuestosApiClient fake)
        => WithOverrides(puestosApiClient: fake);

    /// <summary>
    /// Convenience helper to swap <see cref="IOcupacionApiClient"/> for a fake
    /// without touching the rest of the configuration surface. Mirror de
    /// <see cref="WithPuestosApiClient"/>; agregado en Slice 2 del change
    /// <c>2026-07-28-web-ocupaciones-issue-208</c> (#208) para que la suite
    /// web del módulo Ocupaciones no requiera un backend real.
    /// </summary>
    public SgvWebApplicationFactory WithOcupacionApiClient(IOcupacionApiClient fake)
        => WithOverrides(ocupacionApiClient: fake);

    /// <summary>
    /// Convenience helper to swap <see cref="IVacanteApiClient"/> for a fake
    /// without changing the rest of the web host configuration.
    /// </summary>
    public SgvWebApplicationFactory WithVacanteApiClient(IVacanteApiClient fake)
        => WithOverrides(vacanteApiClient: fake);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            _configureServices?.Invoke(services);

            if (_authApiHandler is not null)
            {
                services.RemoveAll<IAuthApiClient>();
                services.AddTransient<IAuthApiClient>(serviceProvider =>
                {
                    var apiOptions = serviceProvider.GetRequiredService<IOptions<SgvApiOptions>>().Value;
                    var client = new HttpClient(_authApiHandler, disposeHandler: false)
                    {
                        BaseAddress = new Uri(apiOptions.BaseUrl, UriKind.Absolute),
                        Timeout = TimeSpan.FromSeconds(10)
                    };

                    return new AuthApiClient(client);
                });
            }

            if (_cargoApiHandler is not null)
            {
                // Rebuild the cargo typed-client registration with the recording
                // handler as the primary. The ApiBearerTokenHandler stays in the
                // pipeline because it was registered by Program.cs; we only swap
                // the bottom-of-stack transport here so the test can observe what
                // reaches the network layer.
                services.RemoveAll<ICargoApiClient>();
                services.AddHttpClient<ICargoApiClient, CargoApiClient>((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<SgvApiOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                    client.Timeout = TimeSpan.FromSeconds(10);
                })
                .ConfigurePrimaryHttpMessageHandler(() => _cargoApiHandler);
            }

            if (_personaApiHandler is not null)
            {
                // Rebuild the persona typed-client registration with the recording
                // handler as the primary. The ApiBearerTokenHandler stays in the
                // pipeline because it was registered by Program.cs; we only swap
                // the bottom-of-stack transport here so the test can observe what
                // reaches the network layer. Agregado en Slice 2 del change
                // implementa-persona-habilidades para soportar el equivalente
                // de ApiBearerTokenIntegrationTests contra el subrecurso
                // persona-skill.
                services.RemoveAll<IPersonaApiClient>();
                services.AddHttpClient<IPersonaApiClient, PersonaApiClient>((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<SgvApiOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                    client.Timeout = TimeSpan.FromSeconds(10);
                })
                .ConfigurePrimaryHttpMessageHandler(() => _personaApiHandler);
            }

            if (_unidadOrganizativaApiClient is not null)
            {
                services.RemoveAll<IUnidadOrganizativaApiClient>();
                services.AddSingleton(_unidadOrganizativaApiClient);
            }

            if (_cargoApiClient is not null)
            {
                services.RemoveAll<ICargoApiClient>();
                services.AddSingleton(_cargoApiClient);
            }

            if (_habilidadApiClient is not null)
            {
                services.RemoveAll<IHabilidadApiClient>();
                services.AddSingleton(_habilidadApiClient);
            }

            if (_puestosApiClient is not null)
            {
                services.RemoveAll<IPuestosApiClient>();
                services.AddSingleton(_puestosApiClient);
            }

            if (_personaApiClient is not null)
            {
                services.RemoveAll<IPersonaApiClient>();
                services.AddSingleton(_personaApiClient);
            }

            if (_usuarioApiClient is not null)
            {
                services.RemoveAll<IUsuarioApiClient>();
                services.AddSingleton(_usuarioApiClient);
            }

            if (_setupApiClient is not null)
            {
                services.RemoveAll<ISetupApiClient>();
                services.AddSingleton(_setupApiClient);
            }

            if (_ocupacionApiClient is not null)
            {
                services.RemoveAll<IOcupacionApiClient>();
                services.AddSingleton(_ocupacionApiClient);
            }

            if (_vacanteApiClient is not null)
            {
                services.RemoveAll<IVacanteApiClient>();
                services.AddSingleton(_vacanteApiClient);
            }

            if (_recordingLoggerProvider is not null)
            {
                services.AddLogging(builder => builder.AddProvider(_recordingLoggerProvider));
            }

        });
    }
}