namespace SGV.Contracts.Auditoria;

/// <summary>
/// Wire contract seguro para el listado de auditoría. Por diseño
/// NO expone <c>EntityId</c>, <c>OldValuesJson</c> ni
/// <c>NewValuesJson</c> (D-2: evita fuga de PII y mantiene el
/// listado ligero). Sólo metadatos + <c>ChangedPropertiesJson</c>
/// + <c>UserName?</c> (resultado del LEFT JOIN con <c>AspNetUsers</c>;
/// cae a <c>"—"</c> cuando el usuario no existe).
/// </summary>
/// <param name="Id">Identificador único de la fila de auditoría.</param>
/// <param name="EntityName">Nombre lógico de la entidad auditada.</param>
/// <param name="Operation">Operación: Alta, Modificacion, BajaLogica, etc.</param>
/// <param name="OccurredAt">Marca temporal UTC del evento.</param>
/// <param name="UserId">Identificador del usuario que ejecutó la operación.</param>
/// <param name="UserName">
/// Nombre legible del usuario, resuelto vía LEFT JOIN contra
/// <c>AspNetUsers</c>. Devuelve <c>"—"</c> cuando el usuario no
/// existe en Identity (purga, soft-delete, huérfano).
/// </param>
/// <param name="ChangedPropertiesJson">Array JSON con los nombres de las propiedades modificadas (sin valores).</param>
/// <param name="CorrelationId">Identificador de correlación para enlazar operaciones.</param>
public sealed record AuditoriaDto(
    Guid Id,
    string EntityName,
    string Operation,
    DateTime OccurredAt,
    string? UserId,
    string? UserName,
    string? ChangedPropertiesJson,
    Guid? CorrelationId);
