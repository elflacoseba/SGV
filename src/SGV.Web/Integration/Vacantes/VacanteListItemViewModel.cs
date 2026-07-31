using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Row view model for the paginated Vacantes list.
/// </summary>
public sealed record VacanteListItemViewModel(
    Guid Id,
    Guid PuestoId,
    string PuestoNombre,
    Guid EstadoVacanteId,
    string EstadoVacanteNombre,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string Motivo,
    string? Observaciones)
{
    /// <summary>True when the backend supplied a terminal close date.</summary>
    public bool EsCerrada => FechaCierre.HasValue;

    /// <summary>Maps the consumer-safe API DTO into the list view model.</summary>
    public static VacanteListItemViewModel FromDto(VacanteDto dto)
        => new(
            dto.Id,
            dto.PuestoId,
            dto.PuestoNombre,
            dto.EstadoVacanteId,
            dto.EstadoVacanteNombre,
            dto.FechaApertura,
            dto.FechaCierre,
            dto.Motivo,
            dto.Observaciones);
}
