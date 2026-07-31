using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Auditoria;

/// <summary>
/// Cliente HTTP tipado del módulo web de Auditoría (Slice 3 del change
/// <c>implementa-modulo-auditorias</c>). Expone la consulta paginada
/// y el detalle consumidos desde la Razor Page <c>Pages/Auditorias/Index</c>
/// contra los endpoints admin-only <c>GET /api/v1/auditorias</c> y
/// <c>GET /api/v1/auditorias/{id}</c> del backend.
/// </summary>
/// <remarks>
/// Espejo de <c>IPuestosApiClient</c>: el cliente delega la composición de la
/// query URI en <see cref="AuditoriaApiClient.BuildQueryUri"/> (mirror exacto
/// del patrón <c>StringBuilder + Uri.EscapeDataString</c>) y propaga
/// <see cref="HttpRequestException"/> y <see cref="TaskCanceledException"/>
/// nativas, sin traducirlas a un envelope de error, alineado con el spec
/// <c>web-apiclient-transport-contract</c> vigente en el shell web.
/// </remarks>
public interface IAuditoriaApiClient
{
    /// <summary>
    /// Lista auditorías paginadas server-side con los filtros del
    /// <paramref name="query"/>. La implementación traduce 4xx/5xx
    /// nativos vía <c>EnsureSuccessStatusCode</c>; las fallas de
    /// transporte se propagan para que la Razor Page las mapee a un
    /// estado de error recuperable (banner estándar).
    /// </summary>
    /// <param name="query">Filtros + paginación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el detalle de un registro de auditoría por su
    /// identificador único. Devuelve <see langword="null"/> cuando el
    /// backend responde <c>404 Not Found</c> (id desconocido o fila
    /// purgada); propaga <see cref="HttpRequestException"/> y
    /// <see cref="TaskCanceledException"/> para fallos de transporte.
    /// </summary>
    /// <param name="id">Identificador único de la fila de auditoría.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<AuditoriaDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
