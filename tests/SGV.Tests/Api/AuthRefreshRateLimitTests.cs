using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SGV.Contracts.Auth;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Integration;
using SGV.Tests.Persistencia;
using SGV.Tests.Seguridad;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Verifies the <c>Refresh</c> rate-limit policy wired in
/// <c>Program.cs</c> (PR2a of change <c>implementa-refresh-tokens</c>,
/// REQ-AUTH-RATE-1). The quota is lowered through configuration so the
/// test does not need to fire the production default of 20 requests.
/// </summary>
[Collection(MySqlIntegrationCollection.Name)]
public sealed class AuthRefreshRateLimitTests
{
    private const string SigningKey = "E2E-REFRESH-RATE-MIN-32-BYTES-REQUIRED!!!";
    private const int PermitLimit = 3;

    [MySqlFact]
    public async Task Refresh_BeyondPermitLimit_Returns429WithRetryAfter()
    {
        var factory = new QuotaBoundFactory(SigningKey, PermitLimit);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        HttpStatusCode? lastStatus = null;
        HttpResponseMessage? rejected = null;
        for (var attempt = 0; attempt <= PermitLimit; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                AuthApiRoutes.Refresh,
                new RefreshRequest($"quota-probe-{attempt}"));
            lastStatus = response.StatusCode;
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
                break;
            }
            response.Dispose();
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
        Assert.NotNull(rejected);
        Assert.True(
            rejected!.Headers.RetryAfter is not null
            || rejected.Headers.Contains("Retry-After"),
            "429 responses must advertise Retry-After.");
        rejected.Dispose();

        // REQ-AUTH-RATE-1: exhausting the refresh quota must not spill into
        // the login endpoint — AddPolicy creates a separate partition space.
        var login = await client.PostAsJsonAsync(
            AuthApiRoutes.Login,
            new LoginRequest("admin", "Admin#12345"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private sealed class QuotaBoundFactory(string signingKey, int permitLimit)
        : JwtRealWebApplicationFactory(signingKey)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{RefreshTokenOptions.SectionName}:RateLimitPermitLimit"] =
                        permitLimit.ToString(),
                    [$"{RefreshTokenOptions.SectionName}:RateLimitWindowMinutes"] = "15",
                }));
        }
    }
}
