using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace SGV.Infraestructura.Persistencia;

public sealed class SgvDbContextFactory : IDesignTimeDbContextFactory<SgvDbContext>
{
    public SgvDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("SgvDatabase")
            ?? "Server=localhost;Port=3306;Database=SGV;User=root;Password=Flaco1022";
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;

        return new SgvDbContext(opciones);
    }
}
