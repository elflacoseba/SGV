using SGV.Aplicacion.Comun.Persistencia;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class UnitOfWork(SgvDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
