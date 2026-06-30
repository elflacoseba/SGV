using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SGV.Infraestructura.Persistencia;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Builds <see cref="SgvDbContext"/> instances for tests. Unlike the
/// production <see cref="SgvDbContextFactory"/>, it does NOT fail when
/// no connection string is configured — instead it falls back to a
/// reasonable default pointing at a local MySQL on port 3306 so the
/// common dev case (root user, no password) Just Works. Override via
/// the <c>ConnectionStrings__SgvDatabase</c> env var or an
/// <c>appsettings.{Environment}.json</c> on disk if your local setup
/// differs. Tests that actually connect to the database (decorated
/// with <see cref="MySqlFactAttribute"/>) detect the missing server
/// via <c>Database.CanConnect()</c> and skip themselves.
/// </summary>
public sealed class TestSgvDbContextFactory : IDesignTimeDbContextFactory<SgvDbContext>
{
    /// <summary>
    /// Parseable but intentionally non-routable connection string used when
    /// an explicit "fail any DB call" stub is needed (for example to verify
    /// that <see cref="MySqlFactAttribute"/> skips correctly). Pointing at
    /// <c>127.0.0.1:1</c> (a privileged port that always refuses) makes
    /// <c>Database.CanConnect()</c> fail immediately without touching DNS
    /// or any real database.
    /// </summary>
    public const string StubConnectionString =
        "Server=127.0.0.1;Port=1;Database=sgv_stub;User=stub;Password=stub;Default Command Timeout=1;Connection Timeout=1;";

    /// <summary>
    /// Sensible default for local dev with the stock MySQL install
    /// (root user, no password, port 3306). The database name
    /// <c>sgv_test</c> is created on demand by
    /// <see cref="MySqlFactAttribute"/> the first time an integration
    /// test runs against it.
    /// </summary>
    public const string LocalDevConnectionString =
        "Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;Default Command Timeout=30;Connection Timeout=5;";

    public SgvDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        return new SgvDbContext(opciones);
    }

    public static string ResolveConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configured = configuration.GetConnectionString("SgvDatabase");
        return string.IsNullOrWhiteSpace(configured) ? LocalDevConnectionString : configured;
    }
}
