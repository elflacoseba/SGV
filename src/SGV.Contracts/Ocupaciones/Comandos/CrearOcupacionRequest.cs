using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Contracts.Ocupaciones.Comandos;

/// <summary>
/// Request para crear una Ocupación.
/// </summary>
/// <param name="VacanteId">
/// Opcional. Cuando está setado, indica que la Ocupación se crea como
/// cobertura de una Vacante existente; el servicio <c>CrearAsync</c>
/// resuelve <paramref name="PuestoId"/> desde la Vacante (si está vacío)
/// y transiciona la Vacante a <c>Cubierta</c> en la misma transacción.
/// Si ambos (<paramref name="VacanteId"/> y <paramref name="PuestoId"/>)
/// vienen populados, deben coincidir.
/// </param>
public sealed record CrearOcupacionRequest(
    Guid PersonaId,
    Guid PuestoId,
    DateOnly FechaInicio,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones = null,
    Guid? VacanteId = null);
