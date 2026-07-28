using System.Net;
using System.Net.Http.Json;
using System.Text;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Implementación HTTP del cliente de Ocupaciones. Construye queries server-side
/// con <see cref="BuildQueryUri"/> (mismo patrón <c>StringBuilder +
/// Uri.EscapeDataString</c> que <c>PuestosApiClient</c>), respeta
/// <see cref="CancellationToken"/> y propaga fallos de transporte nativos.
/// </summary>
/// <remarks>
/// Slice 3a del change <c>2026-07-28-web-ocupaciones-issue-208</c>: agrega la
/// superficie de mutaciones (Crear/Actualizar/Finalizar/Eliminar/Reactivar)
/// a los métodos de consulta introducidos en Slice 2. La rama no exitosa
/// delega en <see cref="CommandResultMapper"/>; <c>OcupacionError.Categoria</c>
/// viene poblado por el mapper y los PageModels ramifican por categoría.
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

    /// <inheritdoc />
    public async Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<OcupacionDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return OcupacionCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.PutAsJsonAsync(
            $"{BaseRoute}/{id:D}", request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<OcupacionDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return OcupacionCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.PatchAsync(
            $"{BaseRoute}/{id:D}/finalizar",
            JsonContent.Create(request),
            cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<OcupacionDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return OcupacionCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id:D}", cancellationToken)
            .ConfigureAwait(false);

        // 204 No Content = éxito de la baja lógica. El DTO no se devuelve.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new OcupacionCommandResult(true, Value: null, Error: null);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.PatchAsync(
            $"{BaseRoute}/{id:D}/reactivar",
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<OcupacionDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return OcupacionCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Traduce una respuesta HTTP no exitosa a <see cref="OcupacionCommandResult"/>
    /// usando el <see cref="ApiProblemReader"/> y <see cref="CommandResultMapper"/>
    /// comunes. <c>OcupacionError.Categoria</c> queda poblada desde el mapper;
    /// los PageModels ramifican por categoría y conservan el
    /// <see cref="OcupacionError.Code"/> funcional para mostrar el código
    /// específico de unicidad/colisión.
    /// </summary>
    private static async Task<OcupacionCommandResult> ToCommandResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);

        var (categoria, code, message, _) = CommandResultMapper.Map(response, parsed);
        var error = new OcupacionError(categoria, code, message);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return OcupacionCommandResult.Failure(error, parsed.FieldErrors);
        }

        return OcupacionCommandResult.Failure(error);
    }
}