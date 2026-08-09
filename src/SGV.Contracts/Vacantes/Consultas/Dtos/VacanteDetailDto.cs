namespace SGV.Contracts.Vacantes.Consultas.Dtos;

/// <summary>
/// Wire-type consumer-safe del detalle de una vacante, incluyendo su
/// <c>HistorialEstadoVacante</c> en orden cronológico.
/// </summary>
/// <param name="OcupacionDerivadaId">
/// Identificador de la <c>Ocupacion</c> vigente vinculada a esta Vacante.
/// <see langword="null"/> si no existe Ocupación derivada (defensivo: un
/// estado inconsistente Cubierta sin Ocupación resulta en <see langword="null"/>).
/// Hidratado por <c>VacanteServicioConsulta.ObtenerPorIdAsync</c>.
/// </param>
/// <param name="PersonaAsignadaNombre">
/// Nombre completo de la Persona asignada en la Ocupación derivada.
/// <see langword="null"/> si no existe Ocupación derivada o si la
/// Vacante no está Cubierta.
/// </param>
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
    IReadOnlyList<HistorialEstadoVacanteDto> Historial,
    Guid? OcupacionDerivadaId = null,
    string? PersonaAsignadaNombre = null);
