namespace SGV.Contracts.Vacantes.Consultas.Dtos;

/// <summary>
/// Wire-type consumer-safe de una vacante para listados.
/// No expone campos internos de auditoría ni de persistencia.
/// </summary>
public sealed record VacanteDto(
    Guid Id,
    Guid PuestoId,
    string PuestoNombre,
    Guid EstadoVacanteId,
    string EstadoVacanteNombre,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string Motivo,
    string? Observaciones);
