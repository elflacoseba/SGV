using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class HabilidadRepository(SgvDbContext context)
    : ReadOnlyRepository<HabilidadEntity, Habilidad>(context), IHabilidadRepository
{
    protected override IQueryable<HabilidadEntity> Query => base
        .Query
        .Where(h => h.IsActive);

    protected override Habilidad MapToDomain(HabilidadEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(h => h.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }
}
