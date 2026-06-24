using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones.Comandos;

/// <summary>
/// Request to create a new occupation assignment.
/// </summary>
public sealed record CrearOcupacionRequest(
    Guid PersonaId,
    Guid PuestoId,
    DateOnly FechaInicio,
    TipoAsignacion TipoAsignacion,
    string? Observaciones = null
);

/// <summary>
/// Request to update an existing active occupation.
/// </summary>
public sealed record ActualizarOcupacionRequest(
    Guid PersonaId,
    Guid PuestoId,
    DateOnly FechaInicio,
    TipoAsignacion TipoAsignacion,
    string? Observaciones = null
);

/// <summary>
/// Request to finalize an active occupation with an end date.
/// </summary>
public sealed record FinalizarOcupacionRequest(
    DateOnly FechaFin,
    string? Observaciones = null
);
