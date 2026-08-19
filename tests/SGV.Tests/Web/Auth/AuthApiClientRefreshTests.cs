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

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Tests for the refresh- and logout-aware additions of <see cref="IAuthApiClient"/>.
/// PR3 of change <c>implementa-refresh-tokens</c>. Spec: REQ-AUTH-WIRE-1 (consumer side),
/// REQ-AUTH-COOKIES-1/2 (cookie integration is exercised by the smoke tests).
/// </summary>
[Collection("WebIntegration")]
public sealed class AuthApiClientRefreshTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuthApiClientRefreshTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RefreshAsync_WithValidToken_Returns200AndDeserializesResponse()
    {
        var expectedAccess = "new-access-token";
        var expectedRefresh = "new-refresh-token";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(60);
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(14);
        var handler = new ScriptedRefreshHandler(
            request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new RefreshResponse(
                    expectedAccess, expiresAt, expectedRefresh, refreshExpiresAt))
            });
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await client.RefreshAsync(new RefreshRequest("presented-refresh-token"));

        Assert.NotNull(response);
        Assert.Equal(expectedAccess, response!.AccessToken);
        Assert.Equal(expectedRefresh, response.RefreshToken);
        Assert.Equal(expiresAt, response.ExpiresAt);
        Assert.Equal(refreshExpiresAt, response.RefreshTokenExpiresAt);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/refresh"), captured.LastUri);
        Assert.Equal(HttpMethod.Post, captured.LastMethod);
        Assert.NotNull(captured.LastBody);
        using var body = JsonDocument.Parse(captured.LastBody!);
        Assert.Equal("presented-refresh-token", body.RootElement.GetProperty("refreshToken").GetString());
    }

    [Fact]
    public async Task RefreshAsync_OnUnauthorized_ReturnsNull()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await client.RefreshAsync(new RefreshRequest("revoked-token"));

        Assert.Null(response);
        Assert.Equal(1, captured.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_OnTooManyRequests_ReturnsNull()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await client.RefreshAsync(new RefreshRequest("abusive-token"));

        Assert.Null(response);
        Assert.Equal(1, captured.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_OnServerError_PropagatesHttpRequestException()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.RefreshAsync(new RefreshRequest("any-token")));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(1, captured.CallCount);
    }

    [Fact]
    public async Task LogoutAsync_WithValidRequest_ReturnsTrueOn200()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new LogoutResponse(true))
        });
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var ok = await client.LogoutAsync(new LogoutRequest("presented-refresh-token"));

        Assert.True(ok);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/logout"), captured.LastUri);
        Assert.Equal(HttpMethod.Post, captured.LastMethod);
        Assert.NotNull(captured.LastBody);
        using var body = JsonDocument.Parse(captured.LastBody!);
        Assert.Equal("presented-refresh-token", body.RootElement.GetProperty("refreshToken").GetString());
    }

    [Fact]
    public async Task LogoutAsync_WithNullRefreshToken_PresentsNullInBody()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new LogoutResponse(true))
        });
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var ok = await client.LogoutAsync(new LogoutRequest(null));

        Assert.True(ok);
        Assert.NotNull(captured.LastBody);
        using var body = JsonDocument.Parse(captured.LastBody!);
        // RefreshToken may be omitted or null; the API must accept both shapes
        // (REQ-AUTH-LOGOUT-1 scenario "sesión legacy").
        if (body.RootElement.TryGetProperty("refreshToken", out var token))
        {
            Assert.True(token.ValueKind == JsonValueKind.Null || token.ValueKind == JsonValueKind.Undefined);
        }
    }

    [Fact]
    public async Task LogoutAsync_OnUnauthorized_ReturnsFalse()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var ok = await client.LogoutAsync(new LogoutRequest("any-token"));

        Assert.False(ok);
    }

    [Fact]
    public async Task LogoutAsync_OnServerError_PropagatesHttpRequestException()
    {
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.LogoutAsync(new LogoutRequest("any-token")));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_OnTooManyRequests_PropagatesHttpRequestException()
    {
        // The logout endpoint does NOT have a rate-limit policy; a 429 is
        // unexpected and SHOULD propagate as HttpRequestException so the
        // caller (LogoutModel) can fail-open and still clean local cookies.
        var handler = new ScriptedRefreshHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var captured = new RequestCapture();
        await using var factory = CreateFactory(handler, captured);
        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.LogoutAsync(new LogoutRequest("any-token")));
    }

    private SgvWebApplicationFactory CreateFactory(HttpMessageHandler handler, RequestCapture capture)
    {
        return _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                services.RemoveAll<IAuthApiClient>();
                services.AddTransient<IAuthApiClient>(serviceProvider =>
                {
                    var baseAddress = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value.BaseUrl;
                    // Wrap the scripted handler with a capturing layer so the
                    // test sees the actual URI/body that AuthApiClient sent.
                    // Both sharedHttpClient instances use the same capturing
                    // wrapper because RefreshAsync uses anonymousHttpClient
                    // while LogoutAsync uses httpClient (auth pipeline).
                    var capturedHandler = new CapturingRefreshHandler(handler, capture);
                    var authenticatedClient = new HttpClient(capturedHandler)
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };
                    var anonymousClient = new HttpClient(capturedHandler)
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };

                    return new AuthApiClient(authenticatedClient, anonymousClient);
                });
            });
    }

    private sealed class ScriptedRefreshHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class CapturingRefreshHandler : DelegatingHandler
    {
        private readonly RequestCapture _capture;

        public CapturingRefreshHandler(HttpMessageHandler inner, RequestCapture capture)
        {
            InnerHandler = inner;
            _capture = capture;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capture.CallCount++;
            _capture.LastUri = request.RequestUri;
            _capture.LastMethod = request.Method;
            _capture.LastAuth = request.Headers.Authorization;
            _capture.LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RequestCapture
    {
        public int CallCount { get; set; }
        public Uri? LastUri { get; set; }
        public HttpMethod? LastMethod { get; set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? LastAuth { get; set; }
        public string? LastBody { get; set; }
    }
}
