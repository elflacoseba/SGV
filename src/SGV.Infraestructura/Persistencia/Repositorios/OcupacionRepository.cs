using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Especificaciones;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Repository for Ocupacion read and write operations using EF Core.
/// Includes Persona and Puesto navigation properties.
/// Default queries return active (non-deleted, non-finalized) rows.
/// </summary>
public sealed class OcupacionRepository(SgvDbContext context)
    : ReadOnlyRepository<OcupacionEntity, Ocupacion>(context), IOcupacionRepository
{
    protected override IQueryable<OcupacionEntity> Query => base
        .Query
        .Include(o => o.Persona)
        .Include(o => o.Puesto);

    protected override Ocupacion MapToDomain(OcupacionEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    /// <summary>
    /// Returns active occupations only (non-deleted and non-finalized).
    /// Ordered by FechaInicio descending (most recent first).
    /// </summary>
    public override async Task<IReadOnlyList<Ocupacion>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .Where(o => o.FechaFin == null)
            .OrderByDescending(o => o.FechaInicio)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    /// <summary>
    /// Returns ALL persisted occupations including finalized and logically deleted.
    /// No soft-delete or active filter is applied.
    /// </summary>
    public async Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Context
            .Set<OcupacionEntity>()
            .AsNoTracking()
            .Include(o => o.Persona)
            .Include(o => o.Puesto)
            .OrderByDescending(o => o.FechaInicio)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    /// <summary>
    /// Queries occupations by segment and optional filters before counting and paging.
    /// </summary>
    public async Task<(IReadOnlyList<Ocupacion> Items, int TotalCount)> QueryAsync(
        OcupacionListQuery request,
        CancellationToken cancellationToken = default)
    {
        // Tipo explícito IQueryable<...> (no var): el patrón include +
        // where preservado requiere asignar múltiples veces y un cambio
        // del tipo inferido haría divergir el query chain. Ambos segmentos
        // (Activas / Eliminadas) consumen los predicados centralizados en
        // OcupacionEntitySpecs para que cualquier evolución de la regla
        // base (por ejemplo, agregar un bound de FechaInicio) se propague
        // automáticamente al complemento sin duplicar la expresión inline.
        IQueryable<OcupacionEntity> query = Context.Set<OcupacionEntity>()
            .AsNoTracking()
            .Include(o => o.Persona)
            .Include(o => o.Puesto);

        query = request.Segmento == OcupacionSegmentoListado.Activas
            ? query.Where(OcupacionEntitySpecs.EsVigente)
            : query.Where(OcupacionEntitySpecs.NoEsVigente);

        if (request.PersonaId is { } personaId)
        {
            query = query.Where(o => o.PersonaId == personaId);
        }

        if (request.PuestoId is { } puestoId)
        {
            query = query.Where(o => o.PuestoId == puestoId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var likePattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(o =>
                EF.Functions.Like(o.Persona.Nombres, likePattern, "\\") ||
                EF.Functions.Like(o.Persona.Apellidos, likePattern, "\\") ||
                EF.Functions.Like(o.Puesto.Nombre, likePattern, "\\") ||
                (o.Observaciones != null && EF.Functions.Like(o.Observaciones, likePattern, "\\")));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        query = request.Sort?.ToLowerInvariant() switch
        {
            OcupacionApiRoutes.SortFechaInicioAsc => query.OrderBy(o => o.FechaInicio),
            OcupacionApiRoutes.SortPersonaAsc => query.OrderBy(o => o.Persona.Apellidos).ThenBy(o => o.Persona.Nombres),
            OcupacionApiRoutes.SortPersonaDesc => query.OrderByDescending(o => o.Persona.Apellidos).ThenByDescending(o => o.Persona.Nombres),
            OcupacionApiRoutes.SortPuestoAsc => query.OrderBy(o => o.Puesto.Nombre),
            OcupacionApiRoutes.SortPuestoDesc => query.OrderByDescending(o => o.Puesto.Nombre),
            _ => query.OrderByDescending(o => o.FechaInicio)
        };

        var entities = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    /// <summary>
    /// Escapes LIKE wildcard characters so user-supplied '%' or '_' are matched
    /// literally. MySQL uses backslash as the escape character by default.
    /// </summary>
    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    public async Task AddAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(ocupacion);
        await Context.Set<OcupacionEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<OcupacionEntity>()
            .Include(o => o.Persona)
            .Include(o => o.Puesto)
            .Where(o => o.Id == id)
            .Where(OcupacionEntitySpecs.EsVigente)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<OcupacionEntity>()
            .Include(o => o.Persona)
            .Include(o => o.Puesto)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<OcupacionEntity>()
            .FirstOrDefaultAsync(o => o.Id == ocupacion.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(OcupacionEntity)} con id {ocupacion.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, ocupacion);
    }

    public async Task<bool> ExistsActiveByPuestoAsync(
        Guid puestoId,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<OcupacionEntity>()
            .Where(o => o.PuestoId == puestoId && o.Id != excludingId)
            .Where(OcupacionEntitySpecs.EsVigente)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsActiveByPersonaYPuestoAsync(
        Guid personaId,
        Guid puestoId,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<OcupacionEntity>()
            .Where(o => o.PersonaId == personaId && o.PuestoId == puestoId && o.Id != excludingId)
            .Where(OcupacionEntitySpecs.EsVigente)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// T1.10 / REQ-OCC-FORM-010 (invertir-flujo-cubrir): cobertura duplicada
    /// por Vacante. Usado por <c>OcupacionServicioComandos.CrearAsync</c>
    /// antes de insertar una Ocupación derivada de <c>VacanteId</c>.
    /// </summary>
    public async Task<bool> ExistsActiveByVacanteAsync(
        Guid vacanteId,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<OcupacionEntity>()
            .Where(o => o.VacanteId == vacanteId)
            .Where(OcupacionEntitySpecs.EsVigente)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// T1.10 / REQ-OCC-FORM-010 (invertir-flujo-cubrir): hidrata el detalle
    /// de Vacante con la Ocupación vigente vinculada (id + nombre completo
    /// de la Persona asignada). Defensivo: retorna <see langword="null"/>
    /// si no existe Ocupación vigente (estado inconsistente).
    /// Proyección SQL para no materializar el grafo completo en memoria.
    /// </summary>
    public async Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(
        Guid vacanteId,
        CancellationToken cancellationToken = default)
    {
        var projection = await Context
            .Set<OcupacionEntity>()
            .AsNoTracking()
            .Where(o => o.VacanteId == vacanteId)
            .Where(OcupacionEntitySpecs.EsVigente)
            .Select(o => new
            {
                o.Id,
                PersonaNombre = o.Persona.Nombres + " " + o.Persona.Apellidos
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return projection is null
            ? null
            : (projection.Id, projection.PersonaNombre);
    }
}
