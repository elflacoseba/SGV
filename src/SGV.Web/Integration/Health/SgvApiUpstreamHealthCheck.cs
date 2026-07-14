using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SGV.Web.Integration.Health;

/// <summary>
/// Health check for SGV.Web readiness. Probes the SGV API upstream by calling
/// <c>GET /health/live</c> via a named <see cref="System.Net.Http.HttpClient"/>
/// registered with <c>Timeout = 3s</c> and without <c>ApiBearerTokenHandler</c>.
/// </summary>
public sealed class SgvApiUpstreamHealthCheck(
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(SgvApiHealthProbeHttpClient.Name);
            using var resp = await client.GetAsync("/health/live", cancellationToken);
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Upstream responded {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Upstream timeout");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy($"Upstream error: {ex.Message}");
        }
    }
}
