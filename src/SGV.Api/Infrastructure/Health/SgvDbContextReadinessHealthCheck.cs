using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SGV.Infraestructura.Persistencia;

namespace SGV.Api.Infrastructure.Health;

/// <summary>
/// Health check for SGV API readiness. Probes MySQL via
/// <c>SgvDbContext.CanConnectAsync</c> using an <see cref="IDbContextFactory{SgvDbContext}"/>
/// so the context is only created when the check runs, not at host startup.
/// This avoids triggering <c>ServerVersion.AutoDetect</c> before the first probe.
/// </summary>
public sealed class SgvDbContextReadinessHealthCheck(
    IDbContextFactory<SgvDbContext> dbContextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("CanConnectAsync returned false.");
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
