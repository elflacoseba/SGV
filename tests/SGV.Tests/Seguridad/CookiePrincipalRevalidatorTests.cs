using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Web.Auth;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Seguridad;

public sealed class CookiePrincipalRevalidatorTests
{
    [Fact]
    public async Task SigueVigenteAsync_WhenApiAcceptsTheBearer_ReturnsTrue()
    {
        var script = ScriptedHandler.Returning(HttpStatusCode.OK);
        var revalidator = Build(script);

        var result = await revalidator.SigueVigenteAsync("user-1", "jwt-token");

        Assert.True(result);
        Assert.Equal("/api/v1/usuarios/user-1", script.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer jwt-token", script.LastRequest.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task SigueVigenteAsync_WhenApiReturnsNotFound_ReturnsFalse()
    {
        var script = ScriptedHandler.Returning(HttpStatusCode.NotFound);
        var revalidator = Build(script);

        var result = await revalidator.SigueVigenteAsync("deleted-user", "jwt-token");

        Assert.False(result);
        Assert.Equal("/api/v1/usuarios/deleted-user", script.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    public async Task SigueVigenteAsync_ApiResponse_PreservesOrRejects(HttpStatusCode status, bool expected)
    {
        // REL-002: 401/403 must hard-reject the cookie (token revoked or
        // account no longer authorized). 5xx is fail-open by design so an
        // upstream outage does not cascade into a forced sign-out storm.
        var script = ScriptedHandler.Returning(status);
        var revalidator = Build(script);

        var result = await revalidator.SigueVigenteAsync("user-1", "jwt-token");

        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task SigueVigenteAsync_TransportFailure_ReturnsTrue_FailOpen(Exception exception)
    {
        // REL-002: transport-level failures (DNS, connect refused, TLS) and
        // call-token-cancellation timeouts must fail-open so the session is
        // preserved while the API is unreachable.
        var script = ScriptedHandler.Throwing(exception);
        var revalidator = Build(script);

        var result = await revalidator.SigueVigenteAsync("user-1", "jwt-token");

        Assert.True(result);
    }

    public static IEnumerable<object[]> TransportFailures =>
    [
        [new HttpRequestException("connection refused")],
        [new TaskCanceledException("timeout")]
    ];

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

    [Fact]
    public async Task ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims()
    {
        // Defense-in-depth contract: session creation now trusts the JWT as
        // the only NameIdentifier source, but a malformed or custom principal
        // could still carry duplicates. The validated JWT-derived ID is the
        // best signal and the only value the API accepts.
        var script = ScriptedHandler.Returning(HttpStatusCode.OK);
        var revalidator = Build(script);
        var jwtDerivedId = Guid.NewGuid().ToString("N");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice@example.test"),
                new Claim(ClaimTypes.NameIdentifier, jwtDerivedId)
            },
            CookieAuthenticationDefaults.AuthenticationScheme));
        var properties = new AuthenticationProperties();
        properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = AuthTokenNames.AccessToken, Value = "jwt-token" }
        });
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme,
            typeof(CookieAuthenticationHandler));
        var context = new CookieValidatePrincipalContext(
            new DefaultHttpContext(),
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, properties, scheme.Name));

        await revalidator.ValidateAsync(context);

        Assert.False(context.ShouldRenew);
        var escaped = Uri.EscapeDataString(jwtDerivedId);
        Assert.Equal($"/api/v1/usuarios/{escaped}",
            script.LastRequest!.RequestUri!.PathAndQuery);
    }

    private static CookiePrincipalRevalidator Build(ScriptedHandler script)
        => new(new ScriptedHttpClientFactory(script), NullLogger<CookiePrincipalRevalidator>.Instance);

    private sealed class ScriptedHttpClientFactory(ScriptedHandler script) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(script) { BaseAddress = new Uri("https://api.test") };
    }

    /// <summary>
    /// Captures the outbound request and produces a deterministic outcome:
    /// either a pre-baked <see cref="HttpResponseMessage"/> or a thrown
    /// exception. Replaces the previous single-status stub so the 5xx and
    /// transport-failure branches of <see cref="CookiePrincipalRevalidator"/>
    /// can be exercised in unit tests.
    /// </summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _outcome;

        private ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> outcome) => _outcome = outcome;

        public HttpRequestMessage? LastRequest { get; private set; }

        public static ScriptedHandler Returning(HttpStatusCode statusCode)
            => new(_ => new HttpResponseMessage(statusCode));

        public static ScriptedHandler Throwing(Exception exception)
            => new(_ => throw exception);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_outcome(request));
        }
    }
}
