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
    // Issue #280: el filtro `!u.IsDeleted` ya viene del `ReadOnlyRepository.Query`
    // base, pero lo declaramos explícito aquí como defensa en profundidad: si el
    // contrato del base cambiara en el futuro, este override seguiría garantizando
    // que el `ListAllAsync` y `GetByIdAsync` heredado NO devuelvan filas con
    // `IsActive = true && IsDeleted = true` (escenario posible tras una migración
    // manual, script de fix o condición de carrera que invierta el estado del
    // soft-delete). Ver escenarios cubiertos por tests `[MySqlFact]` abajo.
    protected override IQueryable<UnidadOrganizativaEntity> Query => base
        .Query
        .Where(u => u.IsActive && !u.IsDeleted)
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
        // Issue #280: `u.Id != excludingId` con `excludingId = null` se reescribe
        // por EF Core como `u.Id <> NULL` (siempre desconocido en SQL de tres
        // valores). Funciona por la semántica de `AnyAsync` (falsy → false), pero
        // depende del proveedor y no está cubierto contra MySQL real. La forma
        // explícita es portable y trivialmente legible.
        return await Context
            .Set<UnidadOrganizativaEntity>()
            .AnyAsync(u =>
                u.Codigo == codigo &&
                u.IsActive &&
                !u.IsDeleted &&
                (excludingId == null || u.Id != excludingId.Value),
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
        string? sort = null,
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

        // Issue #282: defensa en profundidad — aunque la capa de servicio
        // (UnidadOrganizativaServicioConsulta) ya trimea y clampa el search,
        // un caller directo del repo (por ejemplo un futuro gateway o un test)
        // podría saltarse esa guardia. Trim defensivo aquí mantiene el LIKE
        // predecible aunque llegue con whitespace al borde.
        var trimmedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(u => u.Codigo.Contains(trimmedSearch) || u.Nombre.Contains(trimmedSearch));
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

        // Issue #282: el sort se aplica ANTES del Skip/Take para que la
        // paginación respete el orden visible. Valores whitelisted:
        // codigo_asc / codigo_desc / nombre_asc / nombre_desc /
        // tipo_asc / tipo_desc. Cualquier otro valor (incluido null o
        // strings desconocidos) cae al orden por defecto Codigo asc para
        // preservar el contrato existente.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    /// <summary>
    /// Aplica el orden server-side antes del Skip/Take. Whitelist cerrada:
    /// si el caller pasa un valor no soportado (o null), caemos a
    /// <c>Codigo ASC</c> para preservar el contrato existente.
    /// </summary>
    private static IOrderedQueryable<UnidadOrganizativaEntity> ApplySort(
        IQueryable<UnidadOrganizativaEntity> query,
        string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(u => u.Codigo),
            "codigo_asc" => query.OrderBy(u => u.Codigo),
            "nombre_desc" => query.OrderByDescending(u => u.Nombre),
            "nombre_asc" => query.OrderBy(u => u.Nombre),
            "tipo_desc" => query.OrderByDescending(u => u.TipoUnidadOrganizativa != null ? u.TipoUnidadOrganizativa.Nombre : string.Empty),
            "tipo_asc" => query.OrderBy(u => u.TipoUnidadOrganizativa != null ? u.TipoUnidadOrganizativa.Nombre : string.Empty),
            _ => query.OrderBy(u => u.Codigo)
        };
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

        var byId = hierarchy.ToDictionary(n => n.Id, n => n.UnidadPadreId);

        // H-I1 (housekeeping release-readiness UO+Organigrama): acotar el
        // walk a la cadena de ancestros del candidato (O(depth)) en vez de
        // escanear el grafo completo (O(N·depth)) por cada PUT/PATCH.
        //
        // La versión anterior (issue #277) hacía un `foreach` sobre TODAS
        // las filas detectando ciclos en cualquier rama. Eso tenía dos
        // consecuencias no deseadas:
        //   1) round-trip pesado en el camino crítico de escritura (con
        //      10k unidades, ~10⁸ comparaciones por request);
        //   2) un ciclo en una rama TOTALMENTE ajena a la unidad editada
        //      bloqueaba la operación, convirtiendo un dato corrupto
        //      localizado en una caída global del módulo.
        //
        // Defensa en profundidad: si la cadena del candidato forma un
        // ciclo, lanzamos el código canónico "CicloJerarquico" para que el
        // servicio traduzca a 409. Si el ancestor aparece en la cadena
        // antes de un revisita, retornamos true. Si la cadena termina sin
        // tocar al ancestor, retornamos false.
        var visited = new HashSet<Guid>(capacity: 16);
        var currentId = candidateDescendantId;
        while (byId.TryGetValue(currentId, out var parentId) && parentId.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(currentId))
            {
                throw new InvalidOperationException("CicloJerarquico");
            }

            if (parentId.Value == ancestorId)
            {
                return true;
            }

            currentId = parentId.Value;
        }

        return false;
    }
}
