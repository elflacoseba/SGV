using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Persistencia;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

/// <summary>
/// Tests for connection string validation at startup.
/// Verifies that SGV.Api fails loud when ConnectionStrings:SgvDatabase is missing,
/// whitespace, or malformed, and succeeds with a valid connection string.
/// </summary>
[Collection("ApiIntegration")]
public sealed class StartupValidationTests
{
    private readonly ApiIntegrationFixture _fixture;
    public StartupValidationTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    [Fact]
    public async Task HostBuild_ThrowsWhenConnectionStringMissing()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(
            configureConfig: config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SgvDatabase"] = null
                });
            });

        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = factory.CreateClient();
        });

        Assert.Contains("ConnectionStrings:SgvDatabase", ex.Message);
    }

    [Fact]
    public async Task HostBuild_ThrowsWhenWhitespace()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(
            configureConfig: config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SgvDatabase"] = "   "
                });
            });

        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = factory.CreateClient();
        });

        Assert.Contains("ConnectionStrings:SgvDatabase", ex.Message);
    }

    [Fact]
    public async Task HostBuild_ThrowsWhenMalformed_NoServerNoDatabase()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(
            configureConfig: config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SgvDatabase"] = "Server=localhost"
                });
            });

        var ex = Assert.Throws<OptionsValidationException>(() =>
        {
            using var _ = factory.CreateClient();
        });

        Assert.Contains("ConnectionStrings:SgvDatabase", ex.Message);
        Assert.Contains("Server=", ex.Message);
        Assert.Contains("Database=", ex.Message);
    }

    [Fact]
    public async Task HostBuild_WarnsWhenConnectionTimeoutMissing()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(
            configureConfig: config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SgvDatabase"] = "Server=localhost;Database=sgv_test;Uid=root;"
                });
            });

        // Should NOT throw — host should build successfully with just a warning
        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task HostBuild_SucceedsWithValidConnectionString()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(
            configureConfig: config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SgvDatabase"] = "Server=localhost;Database=sgv_test;Uid=root;Connection Timeout=5;"
                });
            });

        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }
}
