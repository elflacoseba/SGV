using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Smoke tests for the SGV.Web Razor Pages shell.
/// These tests verify anonymous users are redirected to sign-in,
/// authenticated users see the dashboard shell, and logout is exposed.
/// </summary>
[Collection("WebIntegration")]
public sealed class WebShellSmokeTests
{
    private readonly WebIntegrationFixture _fixture;

    public WebShellSmokeTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        // Anónimo: usamos factory local porque el lease anónimo del composite
        // dispose la _root al terminar, rompiendo tests hermanos. (Bug
        // documentado en apply-progress de PR 2b-1.)
        using var localFactory = new SgvWebApplicationFactory();
        using var client = localFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_ReturnsDashboardAndLogout()
    {
        await using var lease = await CreateAuthenticatedLeaseAsync();

        var response = await lease.Client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dashboard", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Logout", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign In", content, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<WebClientLease> CreateAuthenticatedLeaseAsync()
        => await _fixture.CreateAuthOnlyLeaseAsync();
}