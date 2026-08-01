namespace SGV.Contracts.Auditoria;

/// <summary>
/// Wire contract enriquecido para el detalle de un registro de
/// auditoría. Es la **única** superficie del sistema que expone
/// <c>EntityId</c>, <c>OldValuesJson</c> y <c>NewValuesJson</c>:
/// la separación física de tipos respecto de <see cref="AuditoriaDto"/>
/// cierra D-2 por construcción — el listado nunca puede arrastrar
/// estos campos.
/// </summary>
/// <remarks>
/// <para>
/// Expuesto únicamente a través de <c>GET /api/v1/auditorias/{id}</c>
/// (controller admin-only con <c>[Authorize(Roles = Administrador)]</c>)
/// y de la Razor Page <c>/auditorias/details?id={guid}</c> (Slice B del
/// change). Los valores <c>OldValuesJson</c>/<c>NewValuesJson</c>
/// pueden ser <c>null</c> para operaciones sin snapshot (p.ej. Alta).
/// </para>
/// <para>
/// <c>OccurredAt</c> es <see cref="DateTime"/> (alineado con la
/// entidad, la columna MySQL y el wire del listado; ver D-5 en
/// <c>design.md</c>). La delta spec indicaba
/// <c>DateTimeOffset</c>; se interpreta como <c>DateTime</c> por
/// consistencia de stack.
/// </para>
/// </remarks>
public sealed record AuditoriaDetalleDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Operation,
    DateTime OccurredAt,
    string? UserId,
    string? UserName,
    Guid? CorrelationId,
    string? ChangedPropertiesJson,
    string? OldValuesJson,
    string? NewValuesJson);
