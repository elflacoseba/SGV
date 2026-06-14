using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class CargoRepository(SgvDbContext context)
    : ReadOnlyRepository<Cargo>(context), ICargoRepository
{
    protected override IQueryable<Cargo> Query => base
        .Query
        .Where(c => c.IsActive);

    public override async Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);
    }
}
