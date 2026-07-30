namespace SGV.Contracts.Vacantes.Consultas.Dtos;

/// <summary>
/// Wire-type consumer-safe del detalle de una vacante, incluyendo su
/// <c>HistorialEstadoVacante</c> en orden cronológico.
/// </summary>
public sealed record VacanteDetailDto(
    Guid Id,
    Guid PuestoId,
    string PuestoNombre,
    Guid EstadoVacanteId,
    string EstadoVacanteNombre,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string Motivo,
    string? Observaciones,
    IReadOnlyList<HistorialEstadoVacanteDto> Historial);
