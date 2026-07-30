namespace SGV.Contracts.Vacantes.Consultas.Dtos;

/// <summary>
/// Entrada del historial de cambios de estado de una vacante.
/// </summary>
public sealed record HistorialEstadoVacanteDto(
    string? EstadoAnteriorNombre,
    string EstadoNuevoNombre,
    DateTime ChangedAt,
    string? ChangedByUserId,
    string? Motivo);
