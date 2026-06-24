using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Ocupacion. Includes related entity summaries
/// (PersonaNombre, PuestoNombre) and a computed Estado string.
/// </summary>
public sealed record OcupacionDto(
    Guid Id,
    Guid PersonaId,
    string PersonaNombre,
    Guid PuestoId,
    string PuestoNombre,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    TipoAsignacion TipoAsignacion,
    string? Observaciones,
    string Estado
);
