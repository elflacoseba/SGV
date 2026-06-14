using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace SGV.Infraestructura.Persistencia;

public sealed class SgvDbContextFactory : IDesignTimeDbContextFactory<SgvDbContext>
{
    public SgvDbContext CreateDbContext(string[] args)
    {
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql("Server=localhost;Port=3306;Database=SGV;User=root;", serverVersion)
            .Options;

        return new SgvDbContext(opciones);
    }
}
