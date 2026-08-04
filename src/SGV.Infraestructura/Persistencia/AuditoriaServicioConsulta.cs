using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Auditoria;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Persistencia;

/// <summary>
/// Implementación EF directa de <see cref="IAuditoriaServicioConsulta"/>
/// sin repositorio intermedio (mismo patrón que la escritura
/// <see cref="AuditoriaServicio"/>). Garantiza por construcción (D-2)
/// que el <c>AuditoriaDto</c> del listado nunca expone
/// <c>OldValuesJson</c>/<c>NewValuesJson</c> ni <c>EntityId</c>: la
/// proyección enumera campo a campo y <c>AuditoriaDetalleDto</c> es
/// un tipo físico separado (única vía para abrir old/new).
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

    /// <summary>
    /// Fallback literal de <c>UserName</c> cuando el LEFT JOIN con
    /// <c>AspNetUsers</c> no encuentra fila (D-5 bis). Rayo em
    /// (U+2014), consistente con el resto del wire contract que
    /// usa el mismo carácter para valores faltantes.
    /// </summary>
    internal const string UserNameFallback = "—";

    /// <summary>
    /// Cap duro de elementos por array en el endpoint
    /// <c>filter-options</c> (spec <c>auditoria-query</c>). Un
    /// <c>DISTINCT</c> grande sobre <c>Auditorias</c> queda acotado
    /// a los primeros 100 en orden alfabético.
    /// </summary>
    internal const int MaxFilterOptionsItems = 100;

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

        // LEFT JOIN contra AspNetUsers para resolver UserName (D-5 bis).
        // Duplicado intencionalmente con GetDetalleDtoAsync: en C#
        // los tipos anónimos no se pueden devolver desde un helper
        // privado manteniendo la inferencia LINQ encadenable, y los
        // value-tuple de C# no entran en expression trees de EF
        // (CS8143). Si la duplicación crece en el futuro, extraer a
        // un record nominal (AuditoriaJoinUsuario) con
        // propiedad-proyección EF. Por ahora, ~6 líneas duplicadas
        // son aceptables y mantienen la compilación simple.
        var origen = from a in context.Auditorias.AsNoTracking()
                     join u in context.Users.AsNoTracking()
                         on a.UserId equals u.Id into uj
                     from u in uj.DefaultIfEmpty()
                     select new { a, u };

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entityName = query.EntityName;
            origen = origen.Where(x => x.a.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            var operation = query.Operation;
            origen = origen.Where(x => x.a.Operation == operation);
        }

        if (query.DateFrom.HasValue)
        {
            var dateFrom = query.DateFrom.Value;
            origen = origen.Where(x => x.a.OccurredAt >= dateFrom);
        }

        if (query.DateTo.HasValue)
        {
            var dateTo = query.DateTo.Value;
            origen = origen.Where(x => x.a.OccurredAt <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            // Issue #251: el filtro de usuario compara contra
            // u.UserName del LEFT JOIN con AspNetUsers (no contra
            // el GUID técnico a.UserId). El guard x.u != null
            // protege la lambda sobre filas huérfanas (LEFT JOIN
            // con DefaultIfEmpty() puede dejar u en null cuando
            // la fila de auditoría referencia un UserId purgado).
            var userName = query.UserName;
            origen = origen.Where(x => x.u != null && x.u.UserName == userName);
        }

        if (query.CorrelationId.HasValue)
        {
            var correlationId = query.CorrelationId.Value;
            origen = origen.Where(x => x.a.CorrelationId == correlationId);
        }

        var totalCount = await origen.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sort server-side dinámico (spec auditoria-sort). Default
        // fecha_desc; valor no reconocido cae al default sin error.
        // ThenByDescending(Id) es tiebreak determinista universal.
        // `var` con inferencia mantiene el tipo anónimo {a, u} a
        // través del switch (todas las ramas son IOrderedQueryable
        // del mismo tipo). EF traduce la query sin tropezar con la
        // creación de tipos no soportados.
        //
        // La clave "usuario" ordena por el nombre legible del
        // usuario (UserName vía LEFT JOIN con AspNetUsers), no por
        // el UserId técnico. Coincide con lo que ve el operador en
        // la columna "Usuario" de la grilla; los huérfanos caen a
        // UserNameFallback ("—") y se ordenan con el resto.
        var ordenado = query.Sort switch
        {
            "fecha_asc" => origen.OrderBy(x => x.a.OccurredAt),
            "fecha_desc" => origen.OrderByDescending(x => x.a.OccurredAt),
            "entidad_asc" => origen.OrderBy(x => x.a.EntityName),
            "entidad_desc" => origen.OrderByDescending(x => x.a.EntityName),
            "operacion_asc" => origen.OrderBy(x => x.a.Operation),
            "operacion_desc" => origen.OrderByDescending(x => x.a.Operation),
            "usuario_asc" => origen.OrderBy(x => x.u != null ? x.u.UserName : UserNameFallback),
            "usuario_desc" => origen.OrderByDescending(x => x.u != null ? x.u.UserName : UserNameFallback),
            "correlacion_asc" => origen.OrderBy(x => x.a.CorrelationId),
            "correlacion_desc" => origen.OrderByDescending(x => x.a.CorrelationId),
            _ => origen.OrderByDescending(x => x.a.OccurredAt)
        };
        ordenado = ordenado.ThenByDescending(x => x.a.Id);

        // Proyección segura (D-2): el `Select` enumera los campos del
        // wire contract; OldValuesJson/NewValuesJson/EntityId nunca se
        // incluyen en `AuditoriaDto` por construcción (tipo físico
        // separado). EF emite sólo las columnas del DTO en el SELECT.
        var items = await ordenado
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditoriaDto(
                x.a.Id,
                x.a.EntityName,
                x.a.Operation,
                x.a.OccurredAt,
                x.a.UserId,
                x.u != null ? x.u.UserName : UserNameFallback,
                x.a.ChangedPropertiesJson,
                x.a.CorrelationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditoriaDto>(items, totalCount, page, pageSize);
    }

    public async Task<AuditoriaDetalleDto?> GetDetalleDtoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Proyección COMPLETA del DTO enriquecido (incluye
        // EntityId + OldValuesJson + NewValuesJson). Esta es la
        // única vía del sistema para arrastrar esos campos al wire
        // (D-2 cerrado por separación de tipos). LEFT JOIN contra
        // AspNetUsers: ver nota de duplicación intencional con
        // QueryAsync arriba — helper compartido pendiente de
        // evolución a record nominal.
        var dto = await (
            from a in context.Auditorias.AsNoTracking()
            join u in context.Users.AsNoTracking()
                on a.UserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            where a.Id == id
            select new AuditoriaDetalleDto(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Operation,
                a.OccurredAt,
                a.UserId,
                u != null ? u.UserName : UserNameFallback,
                a.CorrelationId,
                a.ChangedPropertiesJson,
                a.OldValuesJson,
                a.NewValuesJson))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return dto;
    }

    public async Task<AuditoriaFilterOptions> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        // Dos proyecciones DISTINCT paralelas sobre la tabla
        // Auditorias (sin JOIN con AspNetUsers: el endpoint no expone
        // usuario). El filtro !IsNullOrWhiteSpace descarta null/""/"
        // " antes del DISTINCT para no contaminar el array. El orden
        // es lexicográfico cliente-servidor; el collation MySQL
        // utf8mb4_0900_ai_ci es case-insensitive y accent-insensitive
        // (Cargo y cargo colapsan al mismo bucket). El Take(100)
        // aplica DESPUÉS de Distinct().OrderBy(...) de modo que se
        // devuelven los primeros 100 en orden alfabético.
        //
        // AsNoTracking garantiza D-4: no se persiste nada al leer.
        // Las queries NO se ejecutan en paralelo (no vale la pena a
        // este volumen; EF los serializa en el mismo DbContext y la
        // latencia agregada es despreciable). Si crece el set, hook
        // natural para sliding-cache queda en la interface.
        var entityNames = await context.Auditorias.AsNoTracking()
            .Where(a => !string.IsNullOrWhiteSpace(a.EntityName))
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(n => n)
            .Take(MaxFilterOptionsItems)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var operations = await context.Auditorias.AsNoTracking()
            .Where(a => !string.IsNullOrWhiteSpace(a.Operation))
            .Select(a => a.Operation)
            .Distinct()
            .OrderBy(o => o)
            .Take(MaxFilterOptionsItems)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AuditoriaFilterOptions(entityNames, operations);
    }
}
