namespace SGV.Contracts.Auditoria;

/// <summary>
/// Contrato de query para el listado paginado de auditoría. Todos los
/// filtros son opcionales; omitirlos significa "no filtrar por ese
/// criterio". El ordenamiento es fijo (servidor, no negociable):
/// <c>OccurredAt DESC, Id DESC</c> (D-3).
/// </summary>
/// <param name="Page">Número de página (1-based).</param>
/// <param name="PageSize">Tamaño de página; el servicio lo clampa a <c>[1, 100]</c>.</param>
/// <param name="EntityName">Filtro opcional por nombre de entidad (exacto, case-sensitive en MySQL).</param>
/// <param name="Operation">Filtro opcional por operación (exacto).</param>
/// <param name="DateFrom">Filtro opcional, inclusivo, sobre <c>OccurredAt</c>.</param>
/// <param name="DateTo">Filtro opcional, inclusivo, sobre <c>OccurredAt</c>.</param>
/// <param name="UserId">Filtro opcional por usuario que ejecutó la operación.</param>
public sealed record AuditoriaListQuery(
    int Page = 1,
    int PageSize = 20,
    string? EntityName = null,
    string? Operation = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? UserId = null);