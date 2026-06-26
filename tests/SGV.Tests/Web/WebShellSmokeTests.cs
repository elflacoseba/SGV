using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Smoke tests for the SGV.Web Razor Pages shell.
/// These tests verify the shell loads without demo content,
/// shows SGV branding, and has no authentication UI.
/// </summary>
public sealed class WebShellSmokeTests
    : IClassFixture<SgvWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebShellSmokeTests(SgvWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Index_ReturnsSuccessAndContainsSvgBrand()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // MUST show SGV branding
        Assert.Contains("SGV", content);
    }

    [Fact]
    public async Task Get_Index_NoDemoDashboardContent()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must NOT contain Inspinia demo dashboard content
        Assert.DoesNotContain("Welcome to INSPINIA", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Revenue", content);
        Assert.DoesNotContain("dashboard-projects", content);
    }

    [Fact]
    public async Task Get_Index_NoAuthLinks()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must NOT contain login/sign-in or account auth links
        Assert.DoesNotContain("Sign In", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Log Out", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lock Screen", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_NoDemoNavigationEntries()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must NOT contain demo menu entries like "Authentication", "Layouts", "Icons", "Menu Levels"
        Assert.DoesNotContain("Authentication", content);
        Assert.DoesNotContain("Layout Options", content);
        Assert.DoesNotContain("Menu Levels", content);
        Assert.DoesNotContain("Components", content);
        Assert.DoesNotContain("Disabled Menu", content);
        Assert.DoesNotContain("Special Menu", content);
    }

    [Fact]
    public async Task Get_Index_HasRequiredAssetReferences()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must reference the shared app css and js
        Assert.Contains("/css/app.min.css", content);
        Assert.Contains("/js/app.js", content);
    }

    [Fact]
    public async Task Get_Index_HasLayoutControlsAndNoCommercialLinks()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must have theme settings offcanvas (customizer)
        Assert.Contains("theme-settings-offcanvas", content);

        // Assert — must NOT have commercial Buy Now link
        Assert.DoesNotContain("Buy Now", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wrapmarket", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_NoLanguageOrUserDropdowns()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — must NOT contain language selector or user dropdown
        Assert.DoesNotContain("language-selector", content);
        Assert.DoesNotContain("user-dropdown", content);
        Assert.DoesNotContain("mega-menu", content, StringComparison.OrdinalIgnoreCase);
    }
}
