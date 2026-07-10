using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
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

    public static bool IsRunningInCi()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
    }

    public SgvDbContext CreateDbContext(string[] args)
    {
        return CreateDbContext(ResolveSettings());
    }

    internal static SgvDbContext CreateDbContext(TestMySqlConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(settings.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        return new SgvDbContext(opciones);
    }

    public static string ResolveConnectionString()
        => ResolveSettings().ConnectionString;

    internal static TestMySqlConnectionSettings ResolveSettings()
        => ResolveSettings(configuration: null, environmentConnectionStringOverride: null);

    internal static TestMySqlConnectionSettings ResolveSettings(
        IConfiguration? configuration,
        string? environmentConnectionStringOverride)
    {
        configuration ??= CreateConfiguration();

        var configured = configuration.GetConnectionString("SgvDatabase");

        if (string.IsNullOrWhiteSpace(configured))
        {
            return new TestMySqlConnectionSettings(
                LocalDevConnectionString,
                nameof(LocalDevConnectionString),
                Redact(LocalDevConnectionString));
        }

        const string envVarName = "ConnectionStrings__SgvDatabase";
        environmentConnectionStringOverride ??= Environment.GetEnvironmentVariable(envVarName);
        var source = string.Equals(configured, environmentConnectionStringOverride, StringComparison.Ordinal)
            ? envVarName
            : "appsettings";

        return new TestMySqlConnectionSettings(
            configured,
            source,
            Redact(configured));
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string Redact(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "<redacted>";
        }

        return builder.ConnectionString;
    }
}
