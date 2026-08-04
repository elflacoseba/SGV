using System.Net;
using System.Net.Http.Json;
using System.Text;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Auditoria;

/// <summary>
/// Implementación HTTP de <see cref="IAuditoriaApiClient"/>. Consume
/// los endpoints admin-only del backend
/// (<c>GET /api/v1/auditorias</c> + <c>GET /api/v1/auditorias/{id}</c>)
/// desde el shell web y reusa el wire contract
/// <see cref="AuditoriaListQuery"/> / <see cref="AuditoriaDto"/> /
/// <see cref="AuditoriaDetalleDto"/> de <c>SGV.Contracts.Auditoria</c>.
/// </summary>
/// <remarks>
/// <para>
/// El cliente respeta <see cref="CancellationToken"/>, usa
/// <c>EnsureSuccessStatusCode</c> para traducciones HTTP nativas y
/// distingue <c>404</c> en el detalle para devolver <c>null</c>
/// (mismo patrón que <c>PuestosApiClient.ObtenerPorIdAsync</c> y
/// <c>OcupacionApiClient.ObtenerPorIdAsync</c>).
/// </para>
/// <para>
/// Las excepciones de transporte (<see cref="HttpRequestException"/>,
/// <see cref="TaskCanceledException"/>) se propagan SIN ser
/// envueltas en un <c>CommandResult</c> o equivalente: la
/// PageModel las captura con
/// <see cref="SGV.Web.Integration.Common.TransportFailureClassifier"/>
/// y las traduce a un banner de error recuperable, alineado con el
/// spec <c>web-apiclient-transport-contract</c>.
/// </para>
/// </remarks>
public sealed class AuditoriaApiClient(HttpClient httpClient) : IAuditoriaApiClient
{
    private const string BaseRoute = "/api/v1/auditorias";

    /// <inheritdoc />
    public async Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync(BuildQueryUri(query), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PagedResult<AuditoriaDto>>(cancellationToken)
            .ConfigureAwait(false)
            ?? new PagedResult<AuditoriaDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<AuditoriaDetalleDto?> GetDetalleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient
            .GetAsync($"{BaseRoute}/{id:D}", cancellationToken)
            .ConfigureAwait(false);

        // 404 → estado vacío recuperable para la Razor Page de
        // detalle; cualquier otro status no 2xx sigue propagándose
        // como excepción para que la PageModel muestre un error
        // recuperable. Mismo patrón que
        // PuestosApiClient.ObtenerPorIdAsync y
        // OcupacionApiClient.ObtenerPorIdAsync.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AuditoriaDetalleDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Compone la query string del endpoint <c>GET /api/v1/auditorias</c>
    /// con paginación, filtros opcionales y orden server-side. La
    /// lista completa de claves válidas para <c>sort</c> vive en la
    /// spec <c>auditoria-sort</c>; valor ausente o no reconocido
    /// es responsabilidad del servicio (cae al default
    /// <c>fecha_desc</c> sin error).
    /// </summary>
    private static string BuildQueryUri(AuditoriaListQuery query)
    {
        var builder = new StringBuilder(
            $"{BaseRoute}?page={query.Page}&pageSize={query.PageSize}");

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            builder.Append("&entityName=");
            builder.Append(Uri.EscapeDataString(query.EntityName));
        }

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            builder.Append("&operation=");
            builder.Append(Uri.EscapeDataString(query.Operation));
        }

        if (query.DateFrom.HasValue)
        {
            builder.Append("&dateFrom=");
            builder.Append(Uri.EscapeDataString(query.DateFrom.Value.ToString("o")));
        }

        if (query.DateTo.HasValue)
        {
            builder.Append("&dateTo=");
            builder.Append(Uri.EscapeDataString(query.DateTo.Value.ToString("o")));
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            builder.Append("&userName=");
            builder.Append(Uri.EscapeDataString(query.UserName));
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(query.Sort));
        }

        if (query.CorrelationId.HasValue)
        {
            builder.Append("&correlationId=");
            builder.Append(query.CorrelationId.Value.ToString("D"));
        }

        return builder.ToString();
    }
}
