using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
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
        .Include(u => u.TipoUnidadOrganizativa)
        .Include(u => u.UnidadPadre);

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
            .Include(u => u.UnidadPadre)
            .FirstOrDefaultAsync(u => u.Id == id && u.IsActive && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<UnidadOrganizativaEntity>()
            .Include(u => u.TipoUnidadOrganizativa)
            .Include(u => u.UnidadPadre)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
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

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<UnidadOrganizativaEntity>()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
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
            .Set<UnidadOrganizativaEntity>()
            .AnyAsync(u =>
                u.Codigo == codigo &&
                u.IsActive &&
                !u.IsDeleted &&
                u.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActiveChildrenAsync(Guid unidadId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<UnidadOrganizativaEntity>()
            .AnyAsync(
                u => u.UnidadPadreId == unidadId && u.IsActive && !u.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActivePuestosAsync(Guid unidadId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PuestoEntity>()
            .AnyAsync(
                p => p.UnidadOrganizativaId == unidadId && p.IsActive && !p.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<UnidadOrganizativa> Items, int TotalCount)> QueryAsync(
        string? search,
        Guid? tipoUnidadOrganizativaId,
        Guid? unidadPadreId,
        DateOnly? vigenteEn,
        int page,
        int pageSize,
        UnidadOrganizativaSegmentoListado segmento = UnidadOrganizativaSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UnidadOrganizativaEntity> query = Context
            .Set<UnidadOrganizativaEntity>()
            .AsNoTracking()
            .Where(u => segmento == UnidadOrganizativaSegmentoListado.Activas
                ? (u.IsActive && !u.IsDeleted)
                : (!u.IsActive && u.IsDeleted))
            .Include(u => u.TipoUnidadOrganizativa)
            .Include(u => u.UnidadPadre);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Codigo.Contains(search) || u.Nombre.Contains(search));
        }

        if (tipoUnidadOrganizativaId.HasValue)
        {
            query = query.Where(u => u.TipoUnidadOrganizativaId == tipoUnidadOrganizativaId.Value);
        }

        if (unidadPadreId.HasValue)
        {
            query = query.Where(u => u.UnidadPadreId == unidadPadreId.Value);
        }

        if (vigenteEn.HasValue)
        {
            var date = vigenteEn.Value;
            query = query.Where(u =>
                (!u.VigenteDesde.HasValue || u.VigenteDesde <= date) &&
                (!u.VigenteHasta.HasValue || u.VigenteHasta >= date));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderBy(u => u.Codigo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    public async Task<IReadOnlyList<UnidadOrganizativa>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Context
            .Set<UnidadOrganizativaEntity>()
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted)
            .Include(u => u.TipoUnidadOrganizativa)
            .Include(u => u.UnidadPadre)
            .OrderBy(u => u.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToArray();
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

        // Issue #277: previously this loop used only a sibling check on
        // current.UnidadPadreId against ancestorId and trusted the chain
        // would terminate. If the DB ever stores an active cycle (e.g. a
        // bug allowed it before this fix), the while never terminated and
        // the request thread hung forever. The visited-set bounds the
        // walk to O(depth) in every case and surfaces a clean code
        // ("CicloJerarquico") that the application service translates to
        // 409 Conflict. Capacity is sized to typical hierarchies to avoid
        // rehashing; bound remains O(depth) regardless.
        var visited = new HashSet<Guid>(capacity: 16);
        var current = hierarchy.FirstOrDefault(n => n.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(current.Id))
            {
                throw new InvalidOperationException("CicloJerarquico");
            }

            if (current.UnidadPadreId == ancestorId)
            {
                return true;
            }

            current = hierarchy.FirstOrDefault(n => n.Id == current.UnidadPadreId.Value);
        }

        return false;
    }
}
