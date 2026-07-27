using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class AuthApiClientChangePasswordTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuthApiClientChangePasswordTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ChangePasswordAsync_PostsToAuthenticatedRouteWithExpectedBody()
    {
        var authenticatedHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK));
        await using var factory = CreateFactory(authenticatedHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var result = await client.ChangePasswordAsync(
            new ChangePasswordRequest("Old1Pass!", "New2Pass!", "New2Pass!"));

        Assert.Equal(ChangePasswordOutcome.Success, result);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/change-password"), authenticatedHandler.LastRequestUri);
        Assert.Equal(HttpMethod.Post, authenticatedHandler.LastMethod);
        using var body = JsonDocument.Parse(authenticatedHandler.LastBody!);
        Assert.Equal("Old1Pass!", body.RootElement.GetProperty("currentPassword").GetString());
        Assert.Equal("New2Pass!", body.RootElement.GetProperty("newPassword").GetString());
        Assert.Equal("New2Pass!", body.RootElement.GetProperty("confirmPassword").GetString());
        // El handler autenticado se invoca (no el anónimo).
        Assert.Equal(1, authenticatedHandler.CallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenApiReturnsBadRequest_ReturnsInvalidCurrentPassword()
    {
        var authenticatedHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.BadRequest));
        await using var factory = CreateFactory(authenticatedHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var outcome = await client.ChangePasswordAsync(
            new ChangePasswordRequest("WrongOld!", "New2Pass!", "New2Pass!"));

        Assert.Equal(ChangePasswordOutcome.InvalidCurrentPassword, outcome);
        Assert.Equal(1, authenticatedHandler.CallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenApiReturnsTooManyRequests_ReturnsRateLimited()
    {
        var authenticatedHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await using var factory = CreateFactory(authenticatedHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var outcome = await client.ChangePasswordAsync(
            new ChangePasswordRequest("Old1Pass!", "New2Pass!", "New2Pass!"));

        Assert.Equal(ChangePasswordOutcome.RateLimited, outcome);
        Assert.Equal(1, authenticatedHandler.CallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenApiReturnsServerError_PropagatesHttpRequestException()
    {
        var authenticatedHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var factory = CreateFactory(authenticatedHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ChangePasswordAsync(
                new ChangePasswordRequest("Old1Pass!", "New2Pass!", "New2Pass!")));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(1, authenticatedHandler.CallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCallerAlreadyCancelled_DoesNotSendRequest()
    {
        var authenticatedHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK));
        await using var factory = CreateFactory(authenticatedHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ChangePasswordAsync(
                new ChangePasswordRequest("Old1Pass!", "New2Pass!", "New2Pass!"),
                cancellationTokenSource.Token));

        Assert.Equal(0, authenticatedHandler.CallCount);
    }

    private SgvWebApplicationFactory CreateFactory(HttpMessageHandler authenticatedHandler)
    {
        return _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                services.RemoveAll<IAuthApiClient>();
                services.AddTransient<IAuthApiClient>(serviceProvider =>
                {
                    var baseAddress = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value.BaseUrl;
                    var authenticatedClient = new HttpClient(authenticatedHandler)
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };
                    var anonymousClient = new HttpClient(new NeverCalledHandler())
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };

                    return new AuthApiClient(authenticatedClient, anonymousClient);
                });
            });
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastBody { get; private set; }

        public System.Net.Http.Headers.AuthenticationHeaderValue? LastAuthorization { get; private set; }

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastAuthorization = request.Headers.Authorization;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory();
        }
    }

    /// <summary>
    /// Handler centinela: si el código bajo prueba usa el cliente anónimo por error,
    /// este handler falla el test ruidosamente en lugar de pasar silenciosamente.
    /// </summary>
    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException(
                $"Authenticated endpoint received on anonymous pipeline: {request.Method} {request.RequestUri}");
        }
    }
}