using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Tests for connection string validation at startup.
/// Verifies that SGV.Api fails loud when ConnectionStrings:SgvDatabase is missing,
/// whitespace, or malformed, and succeeds with a valid connection string.
/// </summary>
public sealed class StartupValidationTests
{
    [Fact]
    public void HostBuild_ThrowsWhenConnectionStringMissing()
    {
        using var factory = new ApiWebApplicationFactory(
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
    public void HostBuild_ThrowsWhenWhitespace()
    {
        using var factory = new ApiWebApplicationFactory(
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
    public void HostBuild_ThrowsWhenMalformed_NoServerNoDatabase()
    {
        using var factory = new ApiWebApplicationFactory(
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
    public void HostBuild_WarnsWhenConnectionTimeoutMissing()
    {
        using var factory = new ApiWebApplicationFactory(
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
    public void HostBuild_SucceedsWithValidConnectionString()
    {
        using var factory = new ApiWebApplicationFactory(
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
