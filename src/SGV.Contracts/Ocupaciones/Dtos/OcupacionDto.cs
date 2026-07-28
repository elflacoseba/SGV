using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Contracts.Ocupaciones.Dtos;

public sealed record OcupacionDto(
    Guid Id,
    Guid PersonaId,
    string PersonaNombre,
    Guid PuestoId,
    string PuestoNombre,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones,
    OcupacionEstado Estado);
