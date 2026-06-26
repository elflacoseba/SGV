using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Api.Contracts;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

public sealed class WebAuthenticationTests
{
    [Fact]
    public void AuthApiRoutes_ExposeCentralizedLoginPath()
    {
        Assert.Equal("api/v1/auth", AuthApiRoutes.Base);
        Assert.Equal("login", AuthApiRoutes.LoginRelative);
        Assert.Equal("/api/v1/auth/login", AuthApiRoutes.Login);
    }

    [Fact]
    public async Task LoginAsync_PostsToCentralizedRouteAndReturnsResponse()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);
        using var scope = factory.Services.CreateScope();
        var authApiClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await authApiClient.LoginAsync(new LoginRequest("admin", "Password1!"));

        Assert.NotNull(response);
        Assert.Equal("token-123", response!.AccessToken);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/login"), handler.LastRequestUri);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task LoginAsync_WhenApiReturnsUnauthorized_ReturnsNull()
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);
        using var scope = factory.Services.CreateScope();
        var authApiClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await authApiClient.LoginAsync(new LoginRequest("admin", "bad-password"));

        Assert.Null(response);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/login"), handler.LastRequestUri);
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            return Task.FromResult(response);
        }
    }
}
