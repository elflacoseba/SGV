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
/// N2 (change <c>vacante-ocupacion-flow-alignment</c>):
/// <paramref name="PersonaId"/> es REQUERIDO cuando el estado destino
/// es <c>Cubierta</c>. Provisto por la Postulación ganadora del módulo
/// de Selección (fuera de scope de este change). El servicio lo usa
/// para crear la <c>Ocupacion</c> derivada en la misma transacción EF.
/// </remarks>
public sealed record CambiarEstadoVacanteRequest(
    Guid EstadoVacanteId,
    string? Motivo = null,
    string? Observaciones = null,
    Guid? PersonaId = null);
