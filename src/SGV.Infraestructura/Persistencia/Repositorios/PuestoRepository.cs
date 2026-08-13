using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
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
        // Issue #273 (Slice C): el orden por defecto es por Nombre ascendente
        // con Codigo como tiebreaker estable. Esta consulta alimenta
        // GET /api/v1/puestos (no la paginada QueryAsync, que sigue
        // aceptando ?sort= explícito). El cambio aplica a TODOS los
        // dropdowns que consumen este endpoint: Vacantes/Create,
        // Puestos/Create, Puestos/Edit y Ocupaciones/Create.
        var entities = await Query
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Codigo)
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

    /// <summary>
    /// Server-side paginated query para el módulo Puestos. Construye su
    /// propio <see cref="IQueryable.AsNoTracking"/> con Includes a
    /// <c>UnidadOrganizativa</c> + <c>Cargo</c> (DEC-4: NO reutiliza
    /// <c>Query</c> base, que sólo cubre <c>IsActive</c>). Filtra por
    /// segmento (activas / eliminadas) y por <c>search</c> LIKE sobre
    /// <c>Codigo</c>, <c>Nombre</c> y opcionalmente <c>Descripcion</c>.
    /// Devuelve tupla <c>(Items, TotalCount)</c> (DEC-5).
    /// </summary>
    public async Task<(IReadOnlyList<Puesto> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PuestoSegmentoListado segmento = PuestoSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PuestoEntity> query = Context
            .Set<PuestoEntity>()
            .AsNoTracking()
            .Where(p => segmento == PuestoSegmentoListado.Activas
                ? (p.IsActive && !p.IsDeleted)
                : (!p.IsActive && p.IsDeleted))
            .Include(p => p.UnidadOrganizativa)
            .Include(p => p.Cargo);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Codigo.Contains(search) ||
                p.Nombre.Contains(search) ||
                (p.Descripcion != null && p.Descripcion.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // El sort se aplica ANTES del Skip/Take para que la paginación
        // respete el orden visible (REQ-PTO-001). Valores soportados:
        // codigo_asc / codigo_desc / nombre_asc / nombre_desc. Cualquier
        // otro valor cae al orden por defecto por Codigo asc para
        // preservar contratos existentes.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    private static IOrderedQueryable<PuestoEntity> ApplySort(IQueryable<PuestoEntity> query, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(p => p.Codigo),
            "codigo_asc" => query.OrderBy(p => p.Codigo),
            "nombre_desc" => query.OrderByDescending(p => p.Nombre),
            "nombre_asc" => query.OrderBy(p => p.Nombre),
            _ => query.OrderBy(p => p.Codigo)
        };
    }
}
