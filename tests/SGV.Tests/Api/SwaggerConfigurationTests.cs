using System.Net;
using System.Text.Json;
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

        Assert.Contains("\"SGV API\"", content, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task SwaggerDocument_ListsAllResourcePaths()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        var actualPaths = new HashSet<string>();
        foreach (var path in paths.EnumerateObject())
        {
            actualPaths.Add(path.Name);
        }

        Assert.Contains("/api/v1/unidades-organizativas", actualPaths);
        Assert.Contains("/api/v1/cargos", actualPaths);
        Assert.Contains("/api/v1/puestos", actualPaths);
        Assert.Contains("/api/v1/skills", actualPaths);
    }

    [Fact]
    public async Task NonOrgResources_OnlyExposeGetOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            // Skip organizational-units path which has write operations
            if (path.Name.StartsWith("/api/v1/unidades-organizativas", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.Equal("get", operation.Name, ignoreCase: true);
            }
        }
    }

    [Fact]
    public async Task UnidadesOrganizativas_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/unidades-organizativas");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check parent-change path exposes PATCH
        var parentPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/unidad-padre");
        var parentOps = new HashSet<string>();
        foreach (var op in parentPath.EnumerateObject())
            parentOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", parentOps);
    }
}
