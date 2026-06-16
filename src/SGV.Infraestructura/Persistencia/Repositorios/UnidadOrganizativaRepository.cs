using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class UnidadOrganizativaRepository(SgvDbContext context)
    : ReadOnlyRepository<UnidadOrganizativaEntity, UnidadOrganizativa>(context), IUnidadOrganizativaRepository
{
    protected override IQueryable<UnidadOrganizativaEntity> Query => base
        .Query
        .Where(u => u.IsActive)
        .Include(u => u.TipoUnidadOrganizativa);

    protected override UnidadOrganizativa MapToDomain(UnidadOrganizativaEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(u => u.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToArray();
    }

    public async Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(unidad);
        await Context.Set<UnidadOrganizativaEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<UnidadOrganizativaEntity>()
            .Include(u => u.TipoUnidadOrganizativa)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<UnidadOrganizativaEntity>()
            .FirstOrDefaultAsync(u => u.Id == unidad.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(UnidadOrganizativaEntity)} con id {unidad.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, unidad);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<UnidadOrganizativaEntity>()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = false;
        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDeleted = true;
    }

    public async Task<bool> ExistsActiveCodeAsync(
        string codigo,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<UnidadOrganizativaEntity>()
            .AnyAsync(u =>
                u.Codigo == codigo &&
                u.IsActive &&
                !u.IsDeleted &&
                u.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsDescendantAsync(
        Guid candidateDescendantId,
        Guid ancestorId,
        CancellationToken cancellationToken = default)
    {
        var hierarchy = await Context
            .Set<UnidadOrganizativaEntity>()
            .Where(u => !u.IsDeleted)
            .Select(u => new { u.Id, u.UnidadPadreId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = hierarchy.FirstOrDefault(n => n.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            if (current.UnidadPadreId == ancestorId)
            {
                return true;
            }

            current = hierarchy.FirstOrDefault(n => n.Id == current.UnidadPadreId.Value);
        }

        return false;
    }
}
