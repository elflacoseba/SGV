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

    public async Task AddAsync(Puesto puesto, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(puesto);
        await Context.Set<PuestoEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Puesto?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PuestoEntity>()
            .Include(p => p.UnidadOrganizativa)
            .Include(p => p.Cargo)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && !p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Puesto?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PuestoEntity>()
            .Include(p => p.UnidadOrganizativa)
            .Include(p => p.Cargo)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Puesto puesto, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PuestoEntity>()
            .FirstOrDefaultAsync(p => p.Id == puesto.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(PuestoEntity)} con id {puesto.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, puesto);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PuestoEntity>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
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
            .Set<PuestoEntity>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
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
            .Set<PuestoEntity>()
            .AnyAsync(p =>
                p.Codigo == codigo &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
