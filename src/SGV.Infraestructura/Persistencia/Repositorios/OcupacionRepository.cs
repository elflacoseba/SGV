using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia.Entidades;
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
        var query = Context.Set<OcupacionEntity>()
            .AsNoTracking()
            .Include(o => o.Persona)
            .Include(o => o.Puesto)
            .Where(o => request.Segmento == OcupacionSegmentoListado.Activas
                ? !o.IsDeleted && o.FechaFin == null
                : o.IsDeleted || o.FechaFin != null);

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
            query = query.Where(o =>
                o.Persona.Nombres.Contains(search) ||
                o.Persona.Apellidos.Contains(search) ||
                o.Puesto.Nombre.Contains(search) ||
                (o.Observaciones != null && o.Observaciones.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        query = request.Sort?.ToLowerInvariant() switch
        {
            "fechainicio_asc" => query.OrderBy(o => o.FechaInicio),
            "persona_asc" => query.OrderBy(o => o.Persona.Apellidos).ThenBy(o => o.Persona.Nombres),
            "persona_desc" => query.OrderByDescending(o => o.Persona.Apellidos).ThenByDescending(o => o.Persona.Nombres),
            "puesto_asc" => query.OrderBy(o => o.Puesto.Nombre),
            "puesto_desc" => query.OrderByDescending(o => o.Puesto.Nombre),
            _ => query.OrderByDescending(o => o.FechaInicio)
        };

        var entities = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
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
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted && o.FechaFin == null, cancellationToken)
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
            .AnyAsync(o =>
                o.PuestoId == puestoId &&
                !o.IsDeleted &&
                o.FechaFin == null &&
                o.Id != excludingId,
                cancellationToken)
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
            .AnyAsync(o =>
                o.PersonaId == personaId &&
                o.PuestoId == puestoId &&
                !o.IsDeleted &&
                o.FechaFin == null &&
                o.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
