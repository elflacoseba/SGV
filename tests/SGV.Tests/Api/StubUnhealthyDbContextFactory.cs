using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;

namespace SGV.Tests.Api;

/// <summary>
/// Stub <see cref="IDbContextFactory{SgvDbContext}"/> that creates a context with
/// a connection pointing to a non-existent database, so <c>CanConnectAsync</c>
/// returns false. Used to simulate an unhealthy database for health check tests.
/// </summary>
internal sealed class StubUnhealthyDbContextFactory : IDbContextFactory<SgvDbContext>
{
    public SgvDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Database=nonexistent_test_db;Uid=root;Connection Timeout=1;",
                ServerVersion.AutoDetect("Server=127.0.0.1;Database=nonexistent_test_db;Uid=root;Connection Timeout=1;"))
            .Options;

        return new SgvDbContext(options);
    }

    public Task<SgvDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
