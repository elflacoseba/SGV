using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class CargoRepository(SgvDbContext context)
    : ReadOnlyRepository<CargoEntity, Cargo>(context), ICargoRepository
{
    protected override IQueryable<CargoEntity> Query => base
        .Query
        .Where(c => c.IsActive);

    protected override Cargo MapToDomain(CargoEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }
}
