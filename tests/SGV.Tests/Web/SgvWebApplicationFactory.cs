using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Web.Integration.Auth;

namespace SGV.Tests.Web;

/// <summary>
/// WebApplicationFactory for SGV.Web (Razor Pages shell).
/// Supports service overrides and a fake SGV.Api auth handler for integration tests.
/// </summary>
public sealed class SgvWebApplicationFactory : WebApplicationFactory<SGV.Web.Program>
{
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly HttpMessageHandler? _authApiHandler;

    public SgvWebApplicationFactory()
    {
    }

    private SgvWebApplicationFactory(Action<IServiceCollection>? configureServices, HttpMessageHandler? authApiHandler)
    {
        _configureServices = configureServices;
        _authApiHandler = authApiHandler;
    }

    public SgvWebApplicationFactory WithOverrides(
        Action<IServiceCollection>? configureServices = null,
        HttpMessageHandler? authApiHandler = null)
    {
        return new SgvWebApplicationFactory(configureServices, authApiHandler);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            _configureServices?.Invoke(services);

            if (_authApiHandler is null)
            {
                return;
            }

            services.AddTransient<IAuthApiClient>(serviceProvider =>
            {
                var apiOptions = serviceProvider.GetRequiredService<IOptions<SgvApiOptions>>().Value;
                var client = new HttpClient(_authApiHandler, disposeHandler: false)
                {
                    BaseAddress = new Uri(apiOptions.BaseUrl, UriKind.Absolute)
                };

                return new AuthApiClient(client);
            });
        });
    }
}
