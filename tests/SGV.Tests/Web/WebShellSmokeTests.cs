using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Smoke tests for the SGV.Web Razor Pages shell.
/// These tests verify anonymous users are redirected to sign-in,
/// authenticated users see the dashboard shell, and logout is exposed.
/// </summary>
public sealed class WebShellSmokeTests
    : IClassFixture<SgvWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebShellSmokeTests(SgvWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_ReturnsDashboardAndLogout()
    {
        var authenticatedClient = await CreateAuthenticatedClientAsync();

        var response = await authenticatedClient.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dashboard", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Logout", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign In", content, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
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

        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(signInResponse);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
