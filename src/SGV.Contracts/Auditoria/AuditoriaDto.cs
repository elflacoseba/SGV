namespace SGV.Contracts.Auditoria;

/// <summary>
/// Wire contract seguro para la consulta de auditoría. Por diseño
/// NO expone <c>OldValuesJson</c> ni <c>NewValuesJson</c> para evitar
/// fuga de PII (D-2). Sólo metadatos + <c>ChangedPropertiesJson</c>.
/// </summary>
/// <param name="Id">Identificador único de la fila de auditoría.</param>
/// <param name="EntityName">Nombre lógico de la entidad auditada.</param>
/// <param name="EntityId">Identificador de la instancia auditada (string para abarcar GUIDs, ints y strings).</param>
/// <param name="Operation">Operación: Alta, Modificacion, BajaLogica, etc.</param>
/// <param name="OccurredAt">Marca temporal UTC del evento.</param>
/// <param name="UserId">Identificador del usuario que ejecutó la operación (crudo en v1, D-5).</param>
/// <param name="ChangedPropertiesJson">Array JSON con los nombres de las propiedades modificadas (sin valores).</param>
/// <param name="CorrelationId">Identificador de correlación para enlazar operaciones.</param>
public sealed record AuditoriaDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Operation,
    DateTime OccurredAt,
    string? UserId,
    string? ChangedPropertiesJson,
    Guid? CorrelationId);