using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class AuthApiClientPasswordResetTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuthApiClientPasswordResetTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ForgotPasswordAsync_PostsToAnonymousRouteWithExpectedBody()
    {
        var anonymousHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK));
        await using var factory = CreateFactory(anonymousHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var result = await client.ForgotPasswordAsync(new ForgotPasswordRequest("person@example.com"));

        Assert.Equal(PasswordResetOutcome.Success, result);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/forgot-password"), anonymousHandler.LastRequestUri);
        Assert.Equal(HttpMethod.Post, anonymousHandler.LastMethod);
        using var body = JsonDocument.Parse(anonymousHandler.LastBody!);
        Assert.Equal("person@example.com", body.RootElement.GetProperty("userNameOrEmail").GetString());
        Assert.Null(anonymousHandler.LastAuthorization);
    }

    [Fact]
    public async Task ResetPasswordAsync_PostsToAnonymousRouteWithExpectedBody()
    {
        var anonymousHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK));
        await using var factory = CreateFactory(anonymousHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var result = await client.ResetPasswordAsync(
            new ResetPasswordRequest("user-1", "+a/b=", "Password1!"));

        Assert.Equal(PasswordResetOutcome.Success, result);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/reset-password"), anonymousHandler.LastRequestUri);
        Assert.Equal(HttpMethod.Post, anonymousHandler.LastMethod);
        using var body = JsonDocument.Parse(anonymousHandler.LastBody!);
        Assert.Equal("user-1", body.RootElement.GetProperty("userId").GetString());
        Assert.Equal("+a/b=", body.RootElement.GetProperty("token").GetString());
        Assert.Equal("Password1!", body.RootElement.GetProperty("newPassword").GetString());
        Assert.Null(anonymousHandler.LastAuthorization);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenApiReturnsTooManyRequests_PreservesStatusCode()
    {
        var anonymousHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await using var factory = CreateFactory(anonymousHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ForgotPasswordAsync(new ForgotPasswordRequest("person@example.com")));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(1, anonymousHandler.CallCount);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenCallerAlreadyCancelled_DoesNotSendRequest()
    {
        var anonymousHandler = new RecordingHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK));
        await using var factory = CreateFactory(anonymousHandler);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ResetPasswordAsync(
                new ResetPasswordRequest("user-1", "token", "Password1!"),
                cancellationTokenSource.Token));

        Assert.Equal(0, anonymousHandler.CallCount);
    }

    private SgvWebApplicationFactory CreateFactory(HttpMessageHandler anonymousHandler)
    {
        return _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                services.RemoveAll<IAuthApiClient>();
                services.AddTransient<IAuthApiClient>(serviceProvider =>
                {
                    var baseAddress = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value.BaseUrl;
                    var authenticatedClient = new HttpClient(new RecordingHttpMessageHandler(
                        () => new HttpResponseMessage(HttpStatusCode.OK)))
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };
                    var anonymousClient = new HttpClient(anonymousHandler)
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
}
