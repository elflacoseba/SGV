using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Auditoria;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Infraestructura.Persistencia;

/// <summary>
/// Implementación EF directa de <see cref="IAuditoriaServicioConsulta"/>
/// sin repositorio intermedio (mismo patrón que la escritura
/// <see cref="AuditoriaServicio"/>). Garantiza por construcción (D-2)
/// que <c>OldValuesJson</c> y <c>NewValuesJson</c> nunca se proyectan:
/// el <c>Select</c> sólo enumera los campos del wire contract, por lo
/// que EF no emite columnas sensibles en el SQL.
///
/// Garantiza además (D-4) que la consulta no genera auditoría: usa
/// <c>AsNoTracking</c> y nunca invoca <c>SaveChanges</c>.
/// </summary>
public sealed class AuditoriaServicioConsulta(SgvDbContext context)
    : IAuditoriaServicioConsulta
{
    /// <summary>
    /// Máximo permitido para <c>PageSize</c>; cualquier valor mayor
    /// se clampa hacia abajo (D-3).
    /// </summary>
    internal const int MaxPageSize = 100;

    /// <summary>
    /// Mínimo permitido para <c>PageSize</c>; cualquier valor menor
    /// se ajusta hacia arriba (D-3).
    /// </summary>
    internal const int MinPageSize = 1;

    public async Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.DateFrom.HasValue && query.DateTo.HasValue
            && query.DateFrom.Value > query.DateTo.Value)
        {
            throw new ArgumentException(
                $"El rango de fechas es inválido: DateFrom ({query.DateFrom:o}) es posterior a DateTo ({query.DateTo:o}). "
                + "DateFrom debe ser menor o igual a DateTo.",
                nameof(query));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < MinPageSize
            ? MinPageSize
            : (query.PageSize > MaxPageSize ? MaxPageSize : query.PageSize);

        IQueryable<Entidades.AuditoriaEntity> origen = context.Auditorias
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            origen = origen.Where(a => a.EntityName == query.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            origen = origen.Where(a => a.Operation == query.Operation);
        }

        if (query.DateFrom.HasValue)
        {
            var dateFrom = query.DateFrom.Value;
            origen = origen.Where(a => a.OccurredAt >= dateFrom);
        }

        if (query.DateTo.HasValue)
        {
            var dateTo = query.DateTo.Value;
            origen = origen.Where(a => a.OccurredAt <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            origen = origen.Where(a => a.UserId == query.UserId);
        }

        var totalCount = await origen.CountAsync(cancellationToken).ConfigureAwait(false);

        // Proyección segura: el `Select` enumera los campos del wire
        // contract; OldValuesJson y NewValuesJson nunca se incluyen,
        // por lo que EF no los emite en el SELECT SQL.
        var items = await origen
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditoriaDto(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Operation,
                a.OccurredAt,
                a.UserId,
                a.ChangedPropertiesJson,
                a.CorrelationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditoriaDto>(items, totalCount, page, pageSize);
    }

    public async Task<AuditoriaDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.Auditorias
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AuditoriaDto(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Operation,
                a.OccurredAt,
                a.UserId,
                a.ChangedPropertiesJson,
                a.CorrelationId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}