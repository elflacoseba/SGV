using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Smoke tests verifying that <see cref="SignInModel.OnPostAsync"/> persists
/// the <c>sgv.rt</c> refresh cookie when the API returns a refresh token
/// (PR3 of change <c>implementa-refresh-tokens</c>).
///
/// Spec: REQ-AUTH-COOKIES-1 (cookie hardening by environment),
/// REQ-AUTH-WIRE-1 (consumer side deserializes refresh token).
/// </summary>
[Collection("WebIntegration")]
public sealed class SignInCookieIssuanceTests
{
    private readonly WebIntegrationFixture _fixture;

    public SignInCookieIssuanceTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SignIn_WhenLoginResponseCarriesRefreshToken_AppendsSgvtCookie()
    {
        var refreshToken = "refresh-token-from-api-login";
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(14);
        var accessToken = AdminJwtTestHelper.BuildUserJwt();
        var accessExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var handler = new ScriptedLoginHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(
                    accessToken,
                    accessExpiresAt,
                    refreshToken,
                    refreshExpiresAt))
            });

        await using var lease = CreateLease(handler);
        var client = lease.Client;

        var get = await client.GetAsync("/auth/sign-in");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(get);

        var post = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        // The redirect itself carries the refresh cookie (the browser drops
        // the cookie on the same response, then follows the 302 to /).
        var setCookie = post.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("|", values)
            : string.Empty;
        Assert.Contains("sgv.rt", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(refreshToken, setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_WhenLoginResponseHasNoRefreshToken_StillSucceedsWithoutCookie()
    {
        // Legacy path: login returns null RefreshToken (e.g. before PR2 or
        // when an old API instance is still around). SGV.Web MUST NOT emit
        // an empty sgv.rt cookie.
        var accessToken = AdminJwtTestHelper.BuildUserJwt();
        var accessExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var handler = new ScriptedLoginHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(accessToken, accessExpiresAt))
            });

        await using var lease = CreateLease(handler);
        var client = lease.Client;

        var get = await client.GetAsync("/auth/sign-in");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(get);

        var post = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var setCookie = post.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("|", values)
            : string.Empty;
        Assert.DoesNotContain("sgv.rt", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private WebClientLease CreateLease(HttpMessageHandler handler)
    {
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(o => o.BaseUrl = "https://api.test");
                // The root factory picks up the default app settings JWT key.
                // Override explicitly to align with AdminJwtTestHelper.SigningKey
                // so the access token issued by the scripted login handler
                // passes AuthSessionFactory.CreatePrincipal validation.
                services.Configure<JwtOptions>(o =>
                {
                    o.SigningKey = AdminJwtTestHelper.SigningKey;
                    o.Issuer = AdminJwtTestHelper.Issuer;
                    o.Audience = AdminJwtTestHelper.Audience;
                });
            },
            authApiHandler: handler);
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        return new WebClientLease(factory, client, new TestSentinel());
    }

    private sealed class ScriptedLoginHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
