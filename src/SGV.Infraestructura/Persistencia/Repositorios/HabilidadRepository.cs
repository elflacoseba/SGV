using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class HabilidadRepository(SgvDbContext context)
    : ReadOnlyRepository<Habilidad>(context), IHabilidadRepository
{
    protected override IQueryable<Habilidad> Query => base
        .Query
        .Where(h => h.IsActive);

    public override async Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .OrderBy(h => h.Codigo)
            .ToListAsync(cancellationToken);
    }
}
