using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Cliente HTTP tipado del módulo web de Ocupaciones. Slice 2 de #208:
/// expone exclusivamente los métodos de consulta (<see cref="ListarAsync"/> y
/// <see cref="ObtenerPorIdAsync"/>). Las mutaciones
/// (<c>Crear/Actualizar/Finalizar/Eliminar/Reactivar</c>) se agregan en Slice 3a
/// para mantener cada PR dentro del budget de review.
/// </summary>
/// <remarks>
/// <para>
/// Espejo de <c>PuestosApiClient</c>: el cliente delega la composición de la
/// query URI en <see cref="BuildQueryUri"/> (mirror exacto del patrón
/// <c>StringBuilder + Uri.EscapeDataString</c>) y propaga
/// <see cref="HttpRequestException"/> y <see cref="TaskCanceledException"/>
/// nativas, sin traducirlas a <c>OcupacionCommandResult</c>, alineado con el
/// spec <c>web-apiclient-transport-contract</c>.
/// </para>
/// <para>
/// El listado manda <c>status=eliminadas</c> solo cuando el segmento es
/// <see cref="OcupacionSegmentoListado.Eliminadas"/>; en
/// <see cref="OcupacionSegmentoListado.Activas"/> omite el parámetro y deja
/// al backend el default vigente.
/// </para>
/// </remarks>
public interface IOcupacionApiClient
{
    /// <summary>
    /// Lista ocupaciones paginadas server-side con segmento, búsqueda, orden
    /// y filtros contextuales (<see cref="OcupacionListQuery.PersonaId"/>,
    /// <see cref="OcupacionListQuery.PuestoId"/>) opcionales.
    /// </summary>
    Task<PagedResult<OcupacionDto>> ListarAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una ocupación por id. Devuelve <see langword="null"/> si el
    /// backend responde <c>404 Not Found</c>; propaga excepciones nativas
    /// para fallos de transporte y timeout.
    /// </summary>
    Task<OcupacionDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}