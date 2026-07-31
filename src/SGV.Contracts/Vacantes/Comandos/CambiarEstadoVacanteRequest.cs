namespace SGV.Contracts.Vacantes.Comandos;

/// <summary>
/// Request para transicionar el estado de una vacante persistiendo
/// simultáneamente un registro en <c>HistorialEstadoVacante</c>.
/// </summary>
/// <remarks>
/// PB-3 confirmado: <paramref name="Motivo"/> es opcional al cerrar.
/// <paramref name="Observaciones"/> es opcional y actualiza el campo
/// <c>Observaciones</c> de la vacante en la misma transacción
/// (OQ-1 aprobada + OQ-3 resuelta).
/// </remarks>
public sealed record CambiarEstadoVacanteRequest(
    Guid EstadoVacanteId,
    string? Motivo = null,
    string? Observaciones = null);
