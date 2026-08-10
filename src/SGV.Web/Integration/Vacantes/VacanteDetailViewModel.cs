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
    IReadOnlyList<HistorialEstadoVacanteDto> Historial,
    Guid? OcupacionDerivadaId = null,
    string? PersonaAsignadaNombre = null)
{
    /// <summary>True when the vacancy has reached a terminal state.</summary>
    public bool EsCerrada => FechaCierre.HasValue;

    /// <summary>
    /// True when la Vacante admite el flujo "Cubrir Vacante" desde el frontend.
    /// Equivale a NO estar en estado terminal cubrible (<c>Cubierta</c>) ni
    /// cancelable (<c>Cancelada</c>). Cambio del flow <c>invertir-flujo-cubrir</c>
    /// / S3: ver design §D-5.
    /// </summary>
    public bool EsCubrible
    {
        get
        {
            var nombre = EstadoVacanteNombre?.Trim() ?? string.Empty;
            return !nombre.Equals("Cubierta", StringComparison.OrdinalIgnoreCase)
                && !nombre.Equals("Cancelada", StringComparison.OrdinalIgnoreCase);
        }
    }

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
            dto.Historial.OrderBy(item => item.ChangedAt).ToArray(),
            dto.OcupacionDerivadaId,
            dto.PersonaAsignadaNombre);
}
