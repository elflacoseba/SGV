using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace SGV.Infraestructura.Persistencia;

/// <summary>
/// Design-time factory used by `dotnet ef` tools (migrations, scaffolding).
/// Built for explicit configuration: it FAILS LOUD if no connection string
/// is provided. Secrets must never be hardcoded here — configure them via
/// <c>dotnet user-secrets</c> in <c>src/SGV.Api</c> (UserSecretsId is set in
/// the csproj) or via the <c>ConnectionStrings__SgvDatabase</c> environment
/// variable (used by CI).
/// </summary>
public sealed class SgvDbContextFactory : IDesignTimeDbContextFactory<SgvDbContext>
{
    public SgvDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SgvDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró 'ConnectionStrings:SgvDatabase'. "
              + "Configurala con `dotnet user-secrets set \"ConnectionStrings:SgvDatabase\" \"Server=...;...\"` "
              + "desde src/SGV.Api, o vía la variable de entorno ConnectionStrings__SgvDatabase. "
              + "Nunca commitees credenciales reales en appsettings.");
        }

        // Versión externalizable vía `MySql:ServerVersion` (default 8.0.36),
        // misma fuente que el runtime en Program.cs. Diseño fail-loud: versión
        // inválida aborta `dotnet ef` con mensaje explícito.
        var rawServerVersion = configuration["MySql:ServerVersion"] ?? "8.0.36";
        if (!Version.TryParse(rawServerVersion, out var serverVersionNumber)
            || serverVersionNumber.Major <= 0)
        {
            throw new InvalidOperationException(
                $"MySql:ServerVersion '{rawServerVersion}' inválida: "
              + "debe ser un Version parseable (ej. 8.0.36). "
              + "Configurala en appsettings.Development.json "
              + "o vía la variable de entorno MySql__ServerVersion.");
        }
        var serverVersion = new MySqlServerVersion(serverVersionNumber);

        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;

        return new SgvDbContext(opciones);
    }
}
