using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using SGV.Tests.Persistencia;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

/// <summary>
/// Tests for health check endpoints in SGV.Api.
/// Covers liveness (no auth, no deps) and readiness (MySQL-dependent).
/// </summary>
[Collection("ApiIntegration")]
public sealed class HealthTests
{
    private readonly ApiIntegrationFixture _fixture;
    public HealthTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    [Fact]
    public async Task Live_NoAuth_Returns200()
    {
        var factory = _fixture.RootFactory;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [MySqlFact]
    public async Task Ready_MySqlUp_Returns200()
    {
        var factory = _fixture.RootFactory;
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
        // Use a deliberately unreachable connection string so the raw MySQL probe fails fast.
        // The connection string is well-formed (passes startup validation) but points to a
        // closed port, so OpenAsync times out / fails and the check returns Unhealthy.
        var factory = _fixture.RootFactory.WithOverrides(configureConfig: config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SgvDatabase"] =
                    "Server=127.0.0.1;Port=65000;Database=sgv_test_unreachable;User=root;Password=wrong;Connection Timeout=2;"
            });
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
        var factory = _fixture.RootFactory;
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
