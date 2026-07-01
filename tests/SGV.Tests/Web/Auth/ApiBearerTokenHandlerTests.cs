using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Unit tests for <see cref="ApiBearerTokenHandler"/>.
/// Exercises the five observable behaviors required by the SGV.Web
/// to SGV.Api bridge:
/// <list type="bullet">
///   <item>Pass-through when no <see cref="HttpContext"/> is available
///   (background work, startup).</item>
///   <item>No-op when no token is stored.</item>
///   <item>Propagation of the JWT bearer header when a token is stored.</item>
///   <item>Refusal to clobber a pre-existing <c>Authorization</c> header.</item>
///   <item>Transparency for the request itself (URL, method, inner response).</item>
/// </list>
/// </summary>
public sealed class ApiBearerTokenHandlerTests
{
    private const string TokenName = AuthTokenNames.AccessToken;

    [Fact]
    public async Task SendAsync_WhenHttpContextIsNull_DoesNotAddAuthorizationHeader()
    {
        // Arrange: background work or startup means IHttpContextAccessor may legitimately have a null context.
        var accessor = new FakeHttpContextAccessor { HttpContext = null };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/cargos");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert: no Authorization header, but the request still flowed through.
        Assert.Null(request.Headers.Authorization);
        Assert.NotNull(recording.LastRequest);
        Assert.Equal(new Uri("https://api.test/cargos"), recording.LastRequest!.RequestUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WhenTokenStoreIsEmpty_DoesNotAddAuthorizationHeader()
    {
        // Arrange: real HttpContext, but the authentication service has no token to return.
        var accessor = new FakeHttpContextAccessor
        {
            HttpContext = BuildAuthenticatedContext(accessToken: null)
        };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/cargos");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert: no Authorization header was injected.
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_WhenTokenStoreHasAccessToken_AddsBearerAuthorizationHeader()
    {
        // Arrange: authenticated request with a stored access_token.
        const string token = "test-jwt-abc123";
        var accessor = new FakeHttpContextAccessor
        {
            HttpContext = BuildAuthenticatedContext(accessToken: token)
        };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/cargos");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert: outgoing request carries "Bearer <token>" exactly.
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(token, request.Headers.Authorization.Parameter);
        Assert.Equal($"Bearer {token}", recording.LastRequestAuthorizationHeader);
    }

    [Fact]
    public async Task SendAsync_WhenRequestAlreadyHasAuthorization_DoesNotOverwrite()
    {
        // Arrange: a caller pre-set the Authorization header (e.g. tests, service-to-service).
        // The handler must defer to the caller instead of clobbering.
        const string token = "test-jwt-abc123";
        var accessor = new FakeHttpContextAccessor
        {
            HttpContext = BuildAuthenticatedContext(accessToken: token)
        };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/cargos");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "preset");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert: caller wins — preset is unchanged.
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("preset", request.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer preset", recording.LastRequestAuthorizationHeader);
    }

    [Fact]
    public async Task SendAsync_PreservesRequestAndPassesResponseThrough()
    {
        // Arrange: even when adding the bearer header, the handler must leave the
        // rest of the request alone and let the inner handler's response flow back.
        const string token = "test-jwt-abc123";
        var accessor = new FakeHttpContextAccessor
        {
            HttpContext = BuildAuthenticatedContext(accessToken: token)
        };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.test/path?x=1")
        {
            Content = new StringContent("{\"k\":\"v\"}")
        };
        request.Headers.Add("X-Custom-Header", "preserved-value");

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(recording.LastRequest);
        Assert.Equal(HttpMethod.Post, recording.LastRequest!.Method);
        Assert.Equal(new Uri("https://api.test/path?x=1"), recording.LastRequest.RequestUri);
        Assert.Contains("preserved-value", recording.LastRequest.Headers.GetValues("X-Custom-Header"));
        Assert.Equal($"Bearer {token}", recording.LastRequestAuthorizationHeader);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_WhenStoredTokenIsWhitespace_DoesNotAddAuthorizationHeader()
    {
        // Arrange: a corrupt or malformed ticket may carry whitespace-only tokens.
        // Whitespace is not a valid bearer value; the handler must refuse to forward
        // an "Authorization: Bearer   " header and let the request fall through.
        var accessor = new FakeHttpContextAccessor
        {
            HttpContext = BuildAuthenticatedContext(accessToken: "   ")
        };
        var recording = new RecordingHandler();
        using var invoker = BuildInvoker(accessor, recording);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/cargos");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    private static HttpMessageInvoker BuildInvoker(IHttpContextAccessor accessor, RecordingHandler recording)
    {
        var handler = new ApiBearerTokenHandler(accessor, NullLogger<ApiBearerTokenHandler>.Instance) { InnerHandler = recording };
        return new HttpMessageInvoker(handler, disposeHandler: true);
    }

    /// <summary>
    /// Builds a <see cref="HttpContext"/> whose <see cref="HttpContext.RequestServices"/>
    /// resolves an <see cref="IAuthenticationService"/> that returns the given
    /// <paramref name="accessToken"/> for <c>access_token</c>. Mirrors the
    /// cookie-auth pipeline where <see cref="PropertiesExtensions.StoreTokens"/>
    /// deposits the JWT on the ticket at sign-in time.
    /// </summary>
    private static HttpContext BuildAuthenticatedContext(string? accessToken)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(_ => new FakeAuthenticationService(accessToken));
        var sp = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext
        {
            RequestServices = sp
        };
        return ctx;
    }

    /// <summary>
    /// Inline test double for <see cref="IHttpContextAccessor"/>: the project
    /// does not depend on Moq, so a tiny concrete class keeps the tests
    /// self-contained.
    /// </summary>
    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    /// <summary>
    /// Minimal <see cref="IAuthenticationService"/> stub that returns either a
    /// successful ticket (with the supplied access_token stored under
    /// <see cref="AuthenticationTokenExtensions"/>) or <see cref="AuthenticateResult.NoResult"/>
    /// when no token is supplied.
    /// </summary>
    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        private readonly string? _accessToken;

        public FakeAuthenticationService(string? accessToken)
        {
            _accessToken = accessToken;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity("fake");
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties();
            properties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = TokenName, Value = _accessToken }
            });
            var ticket = new AuthenticationTicket(principal, properties, scheme ?? "fake");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    /// <summary>
    /// Captures the outgoing <see cref="HttpRequestMessage"/> for assertions
    /// and returns a canned 200 OK so the handler chain completes cleanly.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestAuthorizationHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (request.Headers.Authorization is not null)
            {
                LastRequestAuthorizationHeader = request.Headers.Authorization.ToString();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}