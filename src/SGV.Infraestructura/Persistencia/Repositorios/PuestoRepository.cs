using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class PuestoRepository(SgvDbContext context)
    : ReadOnlyRepository<Puesto>(context), IPuestoRepository
{
    protected override IQueryable<Puesto> Query => base
        .Query
        .Where(p => p.IsActive)
        .Include(p => p.UnidadOrganizativa)
        .Include(p => p.Cargo);

    public override async Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .OrderBy(p => p.Codigo)
            .ToListAsync(cancellationToken);
    }
}
