using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [Fact]
    public async Task Get_SignIn_ReturnsSuccessAndOmitsRecoveryLinks()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/auth/sign-in");

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

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

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
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        using var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

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
    public async Task Post_Logout_ClearsCookieAndRedirectsToSignIn()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var homeResponse = await client.GetAsync("/");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(homeResponse);

        var response = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var afterLogout = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains("/auth/sign-in", afterLogout.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var getResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
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
