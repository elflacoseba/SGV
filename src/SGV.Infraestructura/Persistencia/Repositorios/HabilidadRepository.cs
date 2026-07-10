using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
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

    public async Task<(IReadOnlyList<Habilidad> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        HabilidadSegmentoListado segmento = HabilidadSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        IQueryable<HabilidadEntity> query = Context
            .Set<HabilidadEntity>()
            .AsNoTracking()
            .Where(h => segmento == HabilidadSegmentoListado.Activas
                ? (h.IsActive && !h.IsDeleted)
                : (!h.IsActive && h.IsDeleted));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h =>
                h.Codigo.Contains(search) ||
                h.Nombre.Contains(search) ||
                (h.Categoria != null && h.Categoria.Contains(search)) ||
                (h.Descripcion != null && h.Descripcion.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // El sort se aplica ANTES del Skip/Take para que la paginación respete
        // el orden visible (REQ-CM-01 equivalente para habilidades). Valores
        // soportados: codigo_asc / codigo_desc / nombre_asc / nombre_desc /
        // categoria_asc / categoria_desc. Cualquier otro valor cae al orden
        // por defecto por Codigo asc para preservar contratos existentes.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    private static IOrderedQueryable<HabilidadEntity> ApplySort(IQueryable<HabilidadEntity> query, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(h => h.Codigo),
            "codigo_asc" => query.OrderBy(h => h.Codigo),
            "nombre_desc" => query.OrderByDescending(h => h.Nombre),
            "nombre_asc" => query.OrderBy(h => h.Nombre),
            "categoria_desc" => query.OrderByDescending(h => h.Categoria ?? string.Empty),
            "categoria_asc" => query.OrderBy(h => h.Categoria ?? string.Empty),
            _ => query.OrderBy(h => h.Codigo)
        };
    }
}
