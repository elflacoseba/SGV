using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class PuestoRepository(SgvDbContext context)
    : ReadOnlyRepository<PuestoEntity, Puesto>(context), IPuestoRepository
{
    protected override IQueryable<PuestoEntity> Query => base
        .Query
        .Where(p => p.IsActive)
        .Include(p => p.UnidadOrganizativa)
        .Include(p => p.Cargo);

    protected override Puesto MapToDomain(PuestoEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(p => p.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }
}
