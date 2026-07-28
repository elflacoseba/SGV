using System.Net;
using System.Net.Http.Json;
using System.Text;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Implementación HTTP del cliente de Ocupaciones. Construye queries server-side
/// con <see cref="BuildQueryUri"/> (mismo patrón <c>StringBuilder +
/// Uri.EscapeDataString</c> que <c>PuestosApiClient</c>), respeta
/// <see cref="CancellationToken"/> y propaga fallos de transporte nativos.
/// </summary>
/// <remarks>
/// El listado <see cref="ListarAsync"/> exige <see cref="HttpResponseMessage.IsSuccessStatusCode"/>;
/// los códigos no exitosos se propagan al consumidor para que el
/// <c>PageModel</c> los mapee vía <c>CommandResultMapper</c> + <c>PageFeedback</c>
/// cuando corresponda (futuro Slice 3a para mutaciones; Slice 2 sólo lee).
/// </remarks>
public sealed class OcupacionApiClient(HttpClient httpClient) : IOcupacionApiClient
{
    private const string BaseRoute = "/api/v1/ocupaciones";

    /// <inheritdoc />
    public async Task<PagedResult<OcupacionDto>> ListarAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.GetAsync(BuildQueryUri(query), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PagedResult<OcupacionDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? new PagedResult<OcupacionDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<OcupacionDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.GetAsync($"{BaseRoute}/{id:D}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<OcupacionDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Compone la query string del endpoint
    /// <c>GET /api/v1/ocupaciones</c> con segmento, búsqueda, orden,
    /// filtros contextuales y paginación. El parámetro <c>status</c> se
    /// serializa sólo cuando <see cref="OcupacionListQuery.Segmento"/> es
    /// <see cref="OcupacionSegmentoListado.Eliminadas"/> (paridad con
    /// <c>PuestosApiClient</c>).
    /// </summary>
    private static string BuildQueryUri(OcupacionListQuery query)
    {
        var builder = new StringBuilder(
            $"{BaseRoute}?page={query.Page}&pageSize={query.PageSize}");

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(query.Sort));
        }

        if (query.Segmento == OcupacionSegmentoListado.Eliminadas)
        {
            builder.Append("&status=eliminadas");
        }

        if (query.PersonaId.HasValue)
        {
            builder.Append("&personaId=");
            builder.Append(query.PersonaId.Value.ToString("D"));
        }

        if (query.PuestoId.HasValue)
        {
            builder.Append("&puestoId=");
            builder.Append(query.PuestoId.Value.ToString("D"));
        }

        return builder.ToString();
    }
}