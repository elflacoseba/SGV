using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;

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
    private readonly IUnidadOrganizativaApiClient? _unidadOrganizativaApiClient;
    private readonly ICargoApiClient? _cargoApiClient;

    public SgvWebApplicationFactory()
    {
    }

    private SgvWebApplicationFactory(
        Action<IServiceCollection>? configureServices,
        HttpMessageHandler? authApiHandler,
        HttpMessageHandler? cargoApiHandler,
        IUnidadOrganizativaApiClient? unidadOrganizativaApiClient,
        ICargoApiClient? cargoApiClient)
    {
        _configureServices = configureServices;
        _authApiHandler = authApiHandler;
        _cargoApiHandler = cargoApiHandler;
        _unidadOrganizativaApiClient = unidadOrganizativaApiClient;
        _cargoApiClient = cargoApiClient;
    }

    public SgvWebApplicationFactory WithOverrides(
        Action<IServiceCollection>? configureServices = null,
        HttpMessageHandler? authApiHandler = null,
        HttpMessageHandler? cargoApiHandler = null,
        IUnidadOrganizativaApiClient? unidadOrganizativaApiClient = null,
        ICargoApiClient? cargoApiClient = null)
    {
        return new SgvWebApplicationFactory(
            configureServices,
            authApiHandler,
            cargoApiHandler,
            unidadOrganizativaApiClient,
            cargoApiClient);
    }

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
                        BaseAddress = new Uri(apiOptions.BaseUrl, UriKind.Absolute)
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
        });
    }
}