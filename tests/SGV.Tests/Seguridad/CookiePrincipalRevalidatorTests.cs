using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Web.Auth;
using Xunit;

namespace SGV.Tests.Seguridad;

public sealed class CookiePrincipalRevalidatorTests
{
    [Fact]
    public async Task SigueVigenteAsync_WhenApiAcceptsTheBearer_ReturnsTrue()
    {
        var httpClientFactory = new StubHttpClientFactory(HttpStatusCode.OK);
        var revalidator = new CookiePrincipalRevalidator(
            httpClientFactory,
            NullLogger<CookiePrincipalRevalidator>.Instance);

        var result = await revalidator.SigueVigenteAsync("user-1", "jwt-token");

        Assert.True(result);
        Assert.Equal("/api/v1/usuarios/user-1", httpClientFactory.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer jwt-token", httpClientFactory.LastRequest.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task SigueVigenteAsync_WhenApiReturnsNotFound_ReturnsFalse()
    {
        var httpClientFactory = new StubHttpClientFactory(HttpStatusCode.NotFound);
        var revalidator = new CookiePrincipalRevalidator(
            httpClientFactory,
            NullLogger<CookiePrincipalRevalidator>.Instance);

        var result = await revalidator.SigueVigenteAsync("deleted-user", "jwt-token");

        Assert.False(result);
        Assert.Equal("/api/v1/usuarios/deleted-user", httpClientFactory.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public void ValidateAsync_ExposesCookieValidationContextHandler()
    {
        var method = typeof(CookiePrincipalRevalidator).GetMethod(nameof(CookiePrincipalRevalidator.ValidateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(CookieValidatePrincipalContext), parameters[0].ParameterType);
    }

    private sealed class StubHttpClientFactory(HttpStatusCode statusCode) : IHttpClientFactory
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpClient CreateClient(string name)
        {
            var handler = new StubHttpMessageHandler(statusCode, request => LastRequest = request);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.test")
            };
        }
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
