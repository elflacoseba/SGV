using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Auth;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

[Collection("WebIntegration")]
public sealed class WebAuthenticationTests
{
    private readonly WebIntegrationFixture _fixture;

    public WebAuthenticationTests(WebIntegrationFixture fixture) => _fixture = fixture;

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
        var accessToken = AdminJwtTestHelper.BuildUserJwt();
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(accessToken, DateTimeOffset.UtcNow.AddHours(1)))
            });

        await using var lease = CreateAuthLease(handler);
        using var scope = lease.Factory.Services.CreateScope();
        var authApiClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await authApiClient.LoginAsync(new LoginRequest("admin", "Password1!"));

        Assert.NotNull(response);
        Assert.Equal(accessToken, response!.AccessToken);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/login"), handler.LastRequestUri);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    [Fact]
    public async Task LoginAsync_WhenApiReturnsUnauthorized_ReturnsNull()
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var lease = CreateAuthLease(handler);
        using var scope = lease.Factory.Services.CreateScope();
        var authApiClient = scope.ServiceProvider.GetRequiredService<IAuthApiClient>();

        var response = await authApiClient.LoginAsync(new LoginRequest("admin", "bad-password"));

        Assert.Null(response);
        Assert.Equal(new Uri("https://api.test/api/v1/auth/login"), handler.LastRequestUri);
    }

    [Fact]
    public async Task Get_SignIn_ReturnsSuccessAndOmitsRecoveryLinks()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Iniciar sesión", content);
        Assert.DoesNotContain("Forgot Password?", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Create an account", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_SignIn_WithInvalidCredentials_ShowsAuthenticationError()
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await using var lease = CreateAuthLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "bad-password"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenciales inv&#xE1;lidas.", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role=\"alert\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_SignIn_WithValidCredentials_RedirectsToDashboardAndSetsCookie()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(AdminJwtTestHelper.BuildUserJwt(), DateTimeOffset.UtcNow.AddHours(1)))
            });

        await using var lease = CreateAuthLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(new Uri("/", UriKind.Relative), response.Headers.Location);
        Assert.Contains(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : Array.Empty<string>(), value => value.Contains(".AspNetCore.Cookies=", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new Uri("https://api.test/api/v1/auth/login"), handler.LastRequestUri);
    }

    [Fact]
    public async Task Post_SignIn_WhenApiReturnsInvalidToken_ShowsAuthenticationErrorWithoutCookie()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        await using var lease = CreateAuthLease(handler);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : Array.Empty<string>(),
            value => value.Contains(".AspNetCore.Cookies=", StringComparison.OrdinalIgnoreCase));
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("No se pudo validar la sesi&#xF3;n de autenticaci&#xF3;n.", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Logout_ClearsCookieAndRedirectsToSignIn()
    {
        await using var lease = await CreateAuthenticatedLeaseAsync();

        var homeResponse = await lease.Client.GetAsync("/");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(homeResponse);

        var response = await lease.Client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var afterLogout = await lease.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains("/auth/sign-in", afterLogout.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private Task<WebClientLease> CreateAuthenticatedLeaseAsync()
        => _fixture.CreateAuthOnlyLeaseAsync();

    /// <summary>
    /// Construye un lease sobre una factory derivada de la raíz del fixture
    /// con un <see cref="HttpMessageHandler"/> de auth API inyectado. La
    /// factory derivada queda retenida por el lease y se dispone cuando el
    /// scope <c>await using</c> cierra, preservando la regla "ninguna factory
    /// anónima sobrevive al scope" (PR2b-1 §3.5 Approach C).
    ///
    /// Configura tanto <see cref="SgvApiOptions"/> como <see cref="JwtOptions"/>
    /// para mantener la coherencia clave↔token: el JWT servido por el handler
    /// se firma con <see cref="AdminJwtTestHelper.SigningKey"/> y el host debe
    /// validar contra la misma clave. Si se omite <see cref="JwtOptions"/>, la
    /// signing key queda tomada de la configuración del entorno de test
    /// (típicamente <c>dotnet user-secrets</c>) y <see cref="AuthSessionFactory"/>
    /// lanza <c>SecurityTokenSignatureKeyNotFoundException</c>, que el handler
    /// de <c>SignIn.cshtml</c> convierte en 200 con "No se pudo validar la
    /// sesión de autenticación." en lugar del 302 esperado.
    /// </summary>
    private WebClientLease CreateAuthLease(HttpMessageHandler authHandler)
    {
        var factory = _fixture.RootFactory.WithOverrides(
            configureServices: services =>
            {
                services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                services.Configure<JwtOptions>(options =>
                {
                    options.SigningKey = AdminJwtTestHelper.SigningKey;
                    options.Issuer = AdminJwtTestHelper.Issuer;
                    options.Audience = AdminJwtTestHelper.Audience;
                });
            },
            authApiHandler: authHandler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        return new WebClientLease(factory, client, new TestSentinel());
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