namespace SGV.Contracts.Auditoria;

/// <summary>
/// Wire contract inmutable para poblar los <c>&lt;select&gt;</c> de
/// Entidad y Operación del listado de auditoría (issue #251).
/// </summary>
/// <remarks>
/// <para>
/// Por construcción NO porta <c>UserId</c>, <c>UserName</c>,
/// <c>EntityId</c>, <c>OldValuesJson</c> ni <c>NewValuesJson</c>
/// (D-2 reforzado): la separación física de tipos respecto de
/// <see cref="AuditoriaDto"/> y <see cref="AuditoriaDetalleDto"/>
/// impide que el listado pueda arrastrar PII por accidente.
/// </para>
/// <para>
/// Cap duro de 100 elementos por array (recortado en el servicio
/// con <c>Distinct().OrderBy().Take(100)</c>). Valores vacíos o
/// whitespace son descartados antes del <c>DISTINCT</c>.
/// </para>
/// </remarks>
/// <param name="EntityNames">
/// Nombres lógicos de entidades auditadas, ordenados
/// alfabéticamente, sin duplicados, sin strings vacíos. Cap 100.
/// </param>
/// <param name="Operations">
/// Operaciones registradas (Alta, Modificacion, BajaLogica, etc.),
/// ordenadas alfabéticamente, sin duplicados, sin strings vacíos.
/// Cap 100.
/// </param>
public sealed record AuditoriaFilterOptions(
    IReadOnlyList<string> EntityNames,
    IReadOnlyList<string> Operations);