using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SGV.Api.Infrastructure.Health;

/// <summary>
/// Health check for SGV API readiness. Probes MySQL using a raw
/// <see cref="MySqlConnector.MySqlConnection"/> so it never resolves an
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> from the root service provider
/// and never triggers <c>ServerVersion.AutoDetect</c>.
/// </summary>
/// <remarks>
/// The probe opens a connection and immediately closes it. The effective timeout is
/// governed by <c>Connection Timeout</c> in <c>ConnectionStrings:SgvDatabase</c>
/// (MySqlConnector default ~15 s when omitted). A value of 5 s or less is recommended
/// in production so a degraded MySQL fails fast.
/// </remarks>
public sealed class SgvDbContextReadinessHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("SgvDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("ConnectionStrings:SgvDatabase no está configurada.");

        try
        {
            await using var connection = new MySqlConnector.MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MySQL no alcanzable: {ex.Message}");
        }
    }
}
