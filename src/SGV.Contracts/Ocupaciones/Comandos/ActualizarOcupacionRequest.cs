using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Contracts.Ocupaciones.Comandos;

public sealed record ActualizarOcupacionRequest(
    Guid PersonaId,
    Guid PuestoId,
    DateOnly FechaInicio,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones = null);
