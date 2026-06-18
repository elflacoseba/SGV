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

    public async Task AddAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(cargo);
        await Context.Set<CargoEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .Include(c => c.NivelCargo)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && !c.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .Include(c => c.NivelCargo)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == cargo.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(CargoEntity)} con id {cargo.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, cargo);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = false;
        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDeleted = true;
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = true;
        entity.DeletedAt = null;
        entity.IsDeleted = false;
    }

    public async Task<bool> ExistsActiveCodeAsync(
        string codigo,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<CargoEntity>()
            .AnyAsync(c =>
                c.Codigo == codigo &&
                c.IsActive &&
                !c.IsDeleted &&
                c.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PuestoEntity>()
            .AnyAsync(
                p => p.CargoId == cargoId && p.IsActive && !p.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
