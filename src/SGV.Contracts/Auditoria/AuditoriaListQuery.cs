namespace SGV.Contracts.Auditoria;

/// <summary>
/// Contrato de query para el listado paginado de auditoría. Todos los
/// filtros son opcionales; omitirlos significa "no filtrar por ese
/// criterio".
/// </summary>
/// <remarks>
/// El ordenamiento es server-side y dinámico, controlado por
/// <see cref="Sort"/> (ver spec <c>auditoria-sort</c>). Las claves
/// válidas son
/// <c>{fecha|entidad|operacion|usuario|correlacion}_{asc|desc}</c>;
/// <c>null</c> o valor no reconocido cae al default
/// <c>fecha_desc</c> (equivale a <c>OccurredAt DESC, Id DESC</c>).
/// El desempate determinista por <c>Id</c> se aplica SIEMPRE, sin
/// excepción.
/// </remarks>
/// <param name="Page">Número de página (1-based).</param>
/// <param name="PageSize">Tamaño de página; el servicio lo clampa a <c>[1, 100]</c>.</param>
/// <param name="EntityName">Filtro opcional por nombre de entidad (exacto, case-sensitive en MySQL).</param>
/// <param name="Operation">Filtro opcional por operación (exacto).</param>
/// <param name="DateFrom">Filtro opcional, inclusivo, sobre <c>OccurredAt</c>.</param>
/// <param name="DateTo">Filtro opcional, inclusivo, sobre <c>OccurredAt</c>.</param>
/// <param name="UserId">Filtro opcional por usuario que ejecutó la operación.</param>
/// <param name="Sort">
/// Clave de orden server-side (ver <c>auditoria-sort</c>). Default
/// <c>fecha_desc</c>; valor no reconocido cae al default sin error.
/// </param>
/// <param name="CorrelationId">
/// Filtro opcional por <c>CorrelationId</c> exacto (Guid?). Aísla los
/// registros que comparten un mismo identificador de correlación.
/// </param>
public sealed record AuditoriaListQuery(
    int Page = 1,
    int PageSize = 20,
    string? EntityName = null,
    string? Operation = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? UserId = null,
    string? Sort = null,
    Guid? CorrelationId = null);
