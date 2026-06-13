using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SGV.Infraestructura.Persistencia;

public sealed class SgvDbContextFactory : IDesignTimeDbContextFactory<SgvDbContext>
{
    public SgvDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<SgvDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SGV;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new SgvDbContext(opciones);
    }
}
