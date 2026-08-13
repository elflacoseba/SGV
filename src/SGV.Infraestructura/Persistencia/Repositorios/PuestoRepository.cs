using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Especificaciones;
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

    /// <summary>
    /// Devuelve los Puestos activos y no soft-deleted que NO tienen
    /// una Ocupacion vigente (REQ-PTO-DISP-001) NI una Vacante abierta.
    /// Las definiciones de "vigente" y "abierta" viven centralizadas en
    /// <see cref="OcupacionEntitySpecs.EsVigente"/> y
    /// <see cref="VacanteEntitySpecs.EsAbierta"/> para evitar drift
    /// entre el filtro UX y la validación N1/N4.
    /// <para>
    /// Implementación: dos subqueries EXISTS correlacionados contra los
    /// DbSets (no contra las nav collections) porque el overload
    /// <c>Any(Expression&lt;Func&lt;T, bool&gt;&gt;)</c> sólo está
    /// disponible sobre <see cref="IQueryable{T}"/>, y
    /// <c>p.Ocupaciones</c> es <see cref="IEnumerable{T}"/>. Ambos
    /// patrones traducen al mismo SQL <c>NOT EXISTS</c> en MySQL;
    /// usar el DbSet explícito permite componer la
    /// <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c> centralizada sin
    /// envolverla en un lambda in-line.
    /// </para>
    /// Orden estable <c>Nombre ASC, Codigo ASC</c> para alimentar el
    /// dropdown de <c>Vacantes/Create</c>.
    /// </summary>
    public async Task<IReadOnlyList<Puesto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await BuildReadOnlyIQueryable()
            .Where(p => !Context.Set<OcupacionEntity>()
                .Where(o => o.PuestoId == p.Id)
                .Where(OcupacionEntitySpecs.EsVigente)
                .Any())
            .Where(p => !Context.Set<VacanteEntity>()
                .Where(v => v.PuestoId == p.Id)
                .Where(VacanteEntitySpecs.EsAbierta)
                .Any())
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToArray();
    }

    /// <summary>
    /// Raíz read-only reutilizable para métodos de lectura que necesitan
    /// el shape completo de <see cref="PuestoEntity"/> más
    /// <c>UnidadOrganizativa</c> + <c>Cargo</c>:
    /// <c>AsNoTracking + (IsActive &amp;&amp; !IsDeleted) + Includes</c>.
    /// Distinto de <see cref="Query"/> (que sólo aplica <c>IsActive</c> y
    /// es la base de listados administrativos que abarcan eliminados) y
    /// de <see cref="QueryAsync"/> (que necesita filtrar por segmento
    /// activo vs. eliminado). Úselo en métodos nuevos que pidan
    /// "puestos activos-no-borrados con joins de UnidadOrganizativa/Cargo".
    /// </summary>
    private IQueryable<PuestoEntity> BuildReadOnlyIQueryable() =>
        Context.Set<PuestoEntity>()
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Include(p => p.UnidadOrganizativa)
            .Include(p => p.Cargo);
}
