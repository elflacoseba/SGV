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

    public async Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(habilidad);
        await Context.Set<HabilidadEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id && h.IsActive && !h.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == habilidad.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(HabilidadEntity)} con id {habilidad.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, habilidad);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
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
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
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
            .Set<HabilidadEntity>()
            .AnyAsync(h =>
                h.Codigo == codigo &&
                h.IsActive &&
                !h.IsDeleted &&
                h.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
