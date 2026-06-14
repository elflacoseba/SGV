using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SGV.Tests.Api;

public sealed class SwaggerConfigurationTests
    : IClassFixture<WebApplicationFactory<SGV.Api.Program>>
{
    private readonly WebApplicationFactory<SGV.Api.Program> _factory;

    public SwaggerConfigurationTests(WebApplicationFactory<SGV.Api.Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerJsonEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUiPage_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_HasInfoSection()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"SGV Read-Only API\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"v1\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"paths\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiEndpoints_DoNotRequireAuthorization()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        // No security definitions should be present in the OpenAPI document
        // when authentication is disabled.
        Assert.DoesNotContain("security", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearer", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oauth2", content, StringComparison.OrdinalIgnoreCase);
    }
}
