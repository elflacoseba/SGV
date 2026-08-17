using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Typed HTTP client for unidades organizativas endpoints.
/// </summary>
/// <remarks>
/// Slice 2 (#125): este cliente ya no mantiene una matriz privada
/// status→categoría. La rama no exitosa delega en
/// <see cref="CommandResultMapper.Map"/>; los records de error
/// (<see cref="UnidadOrganizativaError"/>,
/// <see cref="UnidadOrganizativaDeleteResult"/>) preservan <c>Categoria</c>
/// poblado por el mapper. El enum legacy
/// (<see cref="UnidadOrganizativaErrorType"/>) se sigue alimentando vía
/// mapeo a-legacy para mantener source-compat durante el ciclo del change.
/// </remarks>
public sealed class UnidadOrganizativaApiClient(HttpClient httpClient) : IUnidadOrganizativaApiClient
{
    private const string BaseRoute = "/api/v1/unidades-organizativas";
    private const string TiposRoute = "/api/v1/tipos-unidad-organizativa";

    /// <inheritdoc />
    public async Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(UnidadOrganizativaListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Status, query.VigenteEn);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<UnidadOrganizativaDto>>(cancellationToken)
            ?? new PagedResult<UnidadOrganizativaDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnidadOrganizativaDto>> GetAllActivasAsync(int pageSize = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var items = new List<UnidadOrganizativaDto>();
        var page = 1;

        while (true)
        {
            var result = await QueryAsync(
                new UnidadOrganizativaListQuery(page, pageSize, Search: null, Sort: null, Status: "activas"),
                cancellationToken);

            if (result.Items.Count == 0)
            {
                break;
            }

            items.AddRange(result.Items);
            if (items.Count >= result.TotalCount)
            {
                break;
            }

            page++;
        }

        return items;
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaArbolResponse> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/arbol", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UnidadOrganizativaArbolResponse>(cancellationToken)
            ?? new UnidadOrganizativaArbolResponse([], []);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TipoUnidadOrganizativaDto>> GetTiposAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(TiposRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TipoUnidadOrganizativaDto>>(cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaCommandResult> CreateAsync(CrearUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaCommandResult> UpdateAsync(Guid id, ActualizarUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaCommandResult> ChangeParentAsync(Guid id, CambiarUnidadPadreRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync($"{BaseRoute}/{id}/unidad-padre", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);
        var result = await DeleteResultMapper.BuildDeleteResultAsync(
            response,
            HttpStatusCode.NoContent,
            cancellationToken);

        return new UnidadOrganizativaDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    private static async Task<UnidadOrganizativaCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacyType(categoria);
        var error = new UnidadOrganizativaError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return UnidadOrganizativaCommandResult.Failure(error, parsed.FieldErrors);
        }

        return UnidadOrganizativaCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="UnidadOrganizativaErrorType"/>
    /// legacy preservando source-compat: <c>NotFound/Conflict/Validation</c>
    /// son 1-a-1; el resto cae en <see cref="UnidadOrganizativaErrorType.Validation"/>.
    /// </summary>
    private static UnidadOrganizativaErrorType MapCategoriaToLegacyType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => UnidadOrganizativaErrorType.NotFound,
        ErrorCategoria.Conflict => UnidadOrganizativaErrorType.Conflict,
        ErrorCategoria.Validation => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Unauthorized => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Forbidden => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Transport => UnidadOrganizativaErrorType.Validation,
        ErrorCategoria.Unexpected => UnidadOrganizativaErrorType.Validation
    };

    private static string BuildQueryUri(int page, int pageSize, string? search, string? status = null, DateOnly? vigenteEn = null)
    {
        var builder = new StringBuilder($"{BaseRoute}/consulta?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.Append("&status=");
            builder.Append(Uri.EscapeDataString(status));
        }

        if (vigenteEn.HasValue)
        {
            builder.Append("&vigenteEn=");
            builder.Append(Uri.EscapeDataString(vigenteEn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return builder.ToString();
    }
}
