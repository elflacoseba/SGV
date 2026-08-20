using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Setup;
using SGV.Web.Pages.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Smoke tests verifying the fail-open logout flow of
/// <see cref="LogoutModel"/> (PR3 of change <c>implementa-refresh-tokens</c>).
///
/// The page must:
/// <list type="number">
///   <item>Call <c>POST /api/v1/auth/logout</c> with the refresh token
///   from the <c>sgv.rt</c> cookie in the body.</item>
///   <item>Sign out the cookie authentication scheme (sgv.auth).</item>
///   <item>Delete the <c>sgv.rt</c> cookie.</item>
///   <item>Redirect to <c>/auth/sign-in</c>.</item>
/// </list>
///
/// Even when the API logout call fails (network error, 401, 5xx), the page
/// MUST still clean local cookies and redirect — fail-open on the UX
/// (design §3.3, REQ-AUTH-COOKIES-2).
/// </summary>
[Collection("WebIntegration")]
public sealed class LogoutCookieClearingTests
{
    private readonly WebIntegrationFixture _fixture;

    public LogoutCookieClearingTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Logout_WithValidSession_CallsApiLogoutThenClearsCookies()
    {
        var refreshToken = "refresh-token-from-login";
        var apiResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var captured = new RequestCapture();
        await using var lease = CreateLease(apiResponse, captured, refreshToken);
        var client = lease.Client;

        // Authenticate the client so the cookie auth principal is set. The
        // sign-in page renders the antiforgery token in the GET response,
        // which we can reuse for the subsequent POST to logout (the
        // antiforgery cookie is keyed by the user session).
        var signIn = await client.GetAsync("/auth/sign-in");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signIn);
        _ = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        // Reset capture so the LoginAsync call doesn't pollute the assertion.
        captured.CallCount = 0;
        captured.LastUri = null;
        captured.LastMethod = null;
        captured.LastBody = null;

        var post = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var redirectLocation = post.Headers.Location?.ToString() ?? string.Empty;
        Assert.Equal("/auth/sign-in", redirectLocation);

        // The logout API MUST have been called with the refresh token from
        // the cookie in the body.
        Assert.Equal(1, captured.CallCount);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/logout"), captured.LastUri);
        Assert.Equal(HttpMethod.Post, captured.LastMethod);
        Assert.NotNull(captured.LastBody);
        Assert.Contains(refreshToken, captured.LastBody!);

        // The response MUST also clear the sgv.rt cookie.
        var setCookie = post.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("|", values)
            : string.Empty;
        Assert.Contains("sgv.rt", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".AspNetCore.Cookies", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_WithApiFailure_StillClearsLocalCookies()
    {
        var refreshToken = "refresh-token-from-login";
        var apiResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var captured = new RequestCapture();
        await using var lease = CreateLease(apiResponse, captured, refreshToken);
        var client = lease.Client;

        var signIn = await client.GetAsync("/auth/sign-in");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signIn);
        _ = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        var post = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var redirectLocation = post.Headers.Location?.ToString() ?? string.Empty;
        Assert.Equal("/auth/sign-in", redirectLocation);

        var setCookie = post.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("|", values)
            : string.Empty;
        Assert.Contains("sgv.rt", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".AspNetCore.Cookies", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_WithoutRefreshCookie_StillClearsAuthAndRedirects()
    {
        var apiResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var captured = new RequestCapture();
        await using var lease = CreateLease(apiResponse, captured, refreshToken: null);
        var client = lease.Client;

        var signIn = await client.GetAsync("/auth/sign-in");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signIn);
        _ = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        var post = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var setCookie = post.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("|", values)
            : string.Empty;
        Assert.Contains("sgv.rt", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".AspNetCore.Cookies", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private WebClientLease CreateLease(HttpResponseMessage apiResponse, RequestCapture capture, string? refreshToken)
    {
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                services.Configure<JwtOptions>(o =>
                {
                    o.SigningKey = AdminJwtTestHelper.SigningKey;
                    o.Issuer = AdminJwtTestHelper.Issuer;
                    o.Audience = AdminJwtTestHelper.Audience;
                });
                services.Configure<RefreshTokenCookieTestOptions>(o => o.RefreshToken = refreshToken);

                // Replace the cookie accessor with a test-aware implementation
                // that injects the refresh token into the request cookie
                // collection regardless of whether the page sets it.
                services.RemoveAll<IRefreshTokenCookieAccessor>();
                services.AddSingleton<IRefreshTokenCookieAccessor>(serviceProvider =>
                {
                    var env = serviceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                    var accessor = serviceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                    var testOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RefreshTokenCookieTestOptions>>().Value;
                    return new TestRefreshTokenCookieAccessor(
                        accessor,
                        env,
                        testOptions.RefreshToken);
                });

                // Bind IAuthApiClient to a capturing handler that returns
                // the scripted response. The capturing handler reads the
                // request body BEFORE returning, which avoids the buffered
                // dispose race that DelegatingHandler-based wrappers hit
                // when the upstream HttpClient's PostAsJsonAsync completes
                // synchronously in the in-memory transport.
                services.RemoveAll<IAuthApiClient>();
                services.AddTransient<IAuthApiClient>(serviceProvider =>
                {
                    var baseAddress = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SgvApiOptions>>().Value.BaseUrl;
                    var capturingHandler = new CapturingLogoutHandler(apiResponse, capture);
                    var client = new HttpClient(capturingHandler)
                    {
                        BaseAddress = new Uri(baseAddress, UriKind.Absolute)
                    };
                    return new AuthApiClient(client);
                });
            });
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        return new WebClientLease(factory, client, new TestSentinel());
    }

    private sealed class CapturingLogoutHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private readonly RequestCapture _capture;

        public CapturingLogoutHandler(HttpResponseMessage response, RequestCapture capture)
        {
            _response = response;
            _capture = capture;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capture.CallCount++;
            _capture.LastUri = request.RequestUri;
            _capture.LastMethod = request.Method;
            _capture.LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }

    private sealed class RequestCapture
    {
        public int CallCount { get; set; }
        public Uri? LastUri { get; set; }
        public HttpMethod? LastMethod { get; set; }
        public string? LastBody { get; set; }
    }

    /// <summary>
    /// Test override that injects a fixed refresh token into the request
    /// cookie collection so the logout page can read it. Production code
    /// never manipulates the request cookies directly.
    /// </summary>
    private sealed class TestRefreshTokenCookieAccessor : IRefreshTokenCookieAccessor
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly string? _seedToken;

        public TestRefreshTokenCookieAccessor(
            IHttpContextAccessor accessor,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
            string? seedToken)
        {
            _accessor = accessor;
            _env = env;
            _seedToken = seedToken;
        }

        public void Set(string refreshToken, DateTimeOffset expiresAt)
        {
            // The test accessor never emits; the Logout page calls Delete
            // which is the path we actually exercise.
        }

        public string? Get()
        {
            var context = _accessor.HttpContext;
            if (context is null) return null;
            if (context.Request.Cookies.TryGetValue(IRefreshTokenCookieAccessor.CookieName, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
            return _seedToken;
        }

        public void Delete()
        {
            var context = _accessor.HttpContext;
            if (context is null) return;
            context.Response.Cookies.Delete(IRefreshTokenCookieAccessor.CookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = _env.IsDevelopment() ? context.Request.IsHttps : true,
                    Path = "/"
                });
        }
    }

    private sealed class RefreshTokenCookieTestOptions
    {
        public string? RefreshToken { get; set; }
    }
}
