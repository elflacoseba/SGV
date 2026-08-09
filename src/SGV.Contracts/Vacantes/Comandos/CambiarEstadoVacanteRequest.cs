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
/// N2 invertido (change <c>invertir-flujo-cubrir</c>):
/// <paramref name="PersonaId"/> está deprecado. El flujo Cubrir vive
/// ahora en <c>OcupacionServicioComandos.CrearAsync</c> cuando el
/// request incluye <c>VacanteId</c> (REQ-OCC-FORM-010). El endpoint
/// <c>PATCH /api/v1/vacantes/{id}/estado</c> rechaza cualquier destino
/// <c>Cubierta</c> con 400 Validation + código
/// <c>CubrirVacanteRequiereCrearOcupacion</c>; <paramref name="PersonaId"/>
/// se ignora silenciosamente. Se conserva en el record para backward
/// compatibility con clientes cacheados; el código de error legacy
/// <c>PersonaIdRequeridoParaCubrir</c> queda marcado como
/// <c>[Obsolete]</c> en <c>VacanteErrorCodigo</c>.
/// </remarks>
public sealed record CambiarEstadoVacanteRequest(
    Guid EstadoVacanteId,
    string? Motivo = null,
    string? Observaciones = null,
    Guid? PersonaId = null);
