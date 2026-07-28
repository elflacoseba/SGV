using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Contracts.Ocupaciones.Comandos;

public sealed record CrearOcupacionRequest(
    Guid PersonaId,
    Guid PuestoId,
    DateOnly FechaInicio,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones = null);
