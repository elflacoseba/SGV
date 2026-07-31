using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Detail view model for a vacante and its chronological state history.
/// </summary>
public sealed record VacanteDetailViewModel(
    Guid Id,
    Guid PuestoId,
    string PuestoNombre,
    Guid EstadoVacanteId,
    string EstadoVacanteNombre,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string Motivo,
    string? Observaciones,
    IReadOnlyList<HistorialEstadoVacanteDto> Historial)
{
    /// <summary>True when the vacancy has reached a terminal state.</summary>
    public bool EsCerrada => FechaCierre.HasValue;

    /// <summary>Maps and orders history chronologically for display.</summary>
    public static VacanteDetailViewModel FromDto(VacanteDetailDto dto)
        => new(
            dto.Id,
            dto.PuestoId,
            dto.PuestoNombre,
            dto.EstadoVacanteId,
            dto.EstadoVacanteNombre,
            dto.FechaApertura,
            dto.FechaCierre,
            dto.Motivo,
            dto.Observaciones,
            dto.Historial.OrderBy(item => item.ChangedAt).ToArray());
}
