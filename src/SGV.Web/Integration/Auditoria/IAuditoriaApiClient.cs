using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Auditoria;

/// <summary>
/// Cliente HTTP tipado del módulo web de Auditoría (cambios
/// `2026-07-31-ajustes-listado-auditoria` y anteriores). Expone la
/// consulta paginada y el detalle consumidos desde la Razor Page
/// <c>Pages/Auditorias/Index</c> contra los endpoints admin-only
/// <c>GET /api/v1/auditorias</c> y <c>GET /api/v1/auditorias/{id}</c>
/// del backend.
/// </summary>
/// <remarks>
/// Espejo de <c>IPuestosApiClient</c>: el cliente delega la composición
/// de la query URI en <see cref="AuditoriaApiClient.BuildQueryUri"/>
/// (mirror exacto del patrón <c>StringBuilder + Uri.EscapeDataString</c>)
/// y propaga <see cref="HttpRequestException"/> y
/// <see cref="TaskCanceledException"/> nativas, sin traducirlas a un
/// envelope de error, alineado con el spec
/// <c>web-apiclient-transport-contract</c> vigente en el shell web.
/// </remarks>
public interface IAuditoriaApiClient
{
    /// <summary>
    /// Lista auditorías paginadas server-side con los filtros y el
    /// orden del <paramref name="query"/>. La implementación
    /// traduce 4xx/5xx nativos vía <c>EnsureSuccessStatusCode</c>;
    /// las fallas de transporte se propagan para que la Razor Page
    /// las mapee a un estado de error recuperable (banner estándar).
    /// </summary>
    /// <param name="query">Filtros + paginación + orden.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el detalle enriquecido de un registro de auditoría
    /// por su identificador único. Devuelve <see langword="null"/>
    /// cuando el backend responde <c>404 Not Found</c> (id
    /// desconocido o fila purgada); propaga
    /// <see cref="HttpRequestException"/> y
    /// <see cref="TaskCanceledException"/> para fallos de transporte.
    /// </summary>
    /// <remarks>
    /// El detalle expone <c>AuditoriaDetalleDto</c> (con
    /// <c>EntityId</c>, <c>OldValuesJson</c> y <c>NewValuesJson</c>),
    /// la única vía del sistema para arrastrar esos campos al wire
    /// (separación física de tipos respecto de <see cref="AuditoriaDto"/>
    /// — D-2).
    /// </remarks>
    /// <param name="id">Identificador único de la fila de auditoría.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<AuditoriaDetalleDto?> GetDetalleAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los valores disponibles para poblar los
    /// <c>&lt;select&gt;</c> de <c>EntityName</c> y <c>Operation</c>
    /// del listado de auditoría (issue #251 / Slice B). Consume el
    /// endpoint admin-only
    /// <c>GET /api/v1/auditorias/filter-options</c> introducido en
    /// Slice A.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La respuesta es un wire seguro: <see cref="AuditoriaFilterOptions"/>
    /// sólo expone los nombres lógicos de entidad y las operaciones
    /// registradas — NO arrastra <c>UserId</c>, <c>UserName</c>,
    /// <c>EntityId</c>, <c>OldValuesJson</c> ni <c>NewValuesJson</c>
    /// (D-2 reforzado por separación física de tipos).
    /// </para>
    /// <para>
    /// Las fallas de transporte se propagan como
    /// <see cref="HttpRequestException"/> o
    /// <see cref="TaskCanceledException"/> para que el PageModel
    /// active la rama de fallback a <c>&lt;input&gt;</c> de texto
    /// sin pintar un error rojo.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<AuditoriaFilterOptions> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default);
}
