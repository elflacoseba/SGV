using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Infraestructura.Persistencia;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Tests for health check endpoints in SGV.Api.
/// Covers liveness (no auth, no deps) and readiness (MySQL-dependent).
/// </summary>
public sealed class HealthTests
{
    [Fact]
    public async Task Live_NoAuth_Returns200()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [MySqlFact]
    public async Task Ready_MySqlUp_Returns200()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.status);
    }

    [Fact]
    public async Task Ready_DbUnhealthy_Returns503()
    {
        var factory = new ApiWebApplicationFactory(configureServices: services =>
        {
            // Remove existing IDbContextFactory<SgvDbContext> (registered by AddDbContextFactory
            // in Program.cs once 0a-GREEN is applied) and add a stub that returns false from
            // CanConnectAsync. The RemoveService is a no-op during RED (no registration yet),
            // but ensures the stub is used when the factory IS registered in GREEN.
            services.RemoveService<IDbContextFactory<SgvDbContext>>();
            services.AddSingleton<IDbContextFactory<SgvDbContext>>(
                new StubUnhealthyDbContextFactory());
        });

        await using (factory)
        {
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
            Assert.NotNull(body);
            Assert.Equal("Unhealthy", body!.status);
        }
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Ready_ResponseHasNoStackTrace(string path)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(body);
        // These fields MUST NOT appear — no exception or stackTrace leak
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stacktrace", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Minimal JSON shape for health check responses.
    /// </summary>
    internal sealed record HealthResponse(string status, double? totalDurationMs, HealthEntry[]? entries);

    internal sealed record HealthEntry(string name, string status, string description, double durationMs);
}
