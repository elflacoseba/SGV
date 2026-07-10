using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Typed HTTP client for unidades organizativas endpoints.
/// </summary>
public sealed class UnidadOrganizativaApiClient(HttpClient httpClient) : IUnidadOrganizativaApiClient
{
    private const string BaseRoute = "/api/v1/unidades-organizativas";
    private const string TiposRoute = "/api/v1/tipos-unidad-organizativa";

    /// <inheritdoc />
    public async Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(UnidadOrganizativaListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Status);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<UnidadOrganizativaDto>>(cancellationToken: cancellationToken)
            ?? new PagedResult<UnidadOrganizativaDto>([], 0, query.Page, query.PageSize);
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
    public async Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/arbol", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<UnidadOrganizativaTreeNodeDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TipoUnidadOrganizativaDto>> GetTiposAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(TiposRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TipoUnidadOrganizativaDto>>(cancellationToken: cancellationToken)
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

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new UnidadOrganizativaDeleteResult(true, response.StatusCode, null, null);
        }

        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);

        return new UnidadOrganizativaDeleteResult(
            false,
            response.StatusCode,
            parsed.Title,
            parsed.Detail);
    }

    private static async Task<UnidadOrganizativaCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            if (parsed.FieldErrors is { Count: > 0 })
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, parsed.Title ?? "ValidationError", parsed.Detail ?? "One or more fields are invalid."),
                    parsed.FieldErrors);
            }

            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, parsed.Title ?? "BadRequest", parsed.Detail ?? "Invalid request."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, parsed.Title ?? "NotFound", parsed.Detail ?? "Resource not found."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, parsed.Title ?? "Conflict", parsed.Detail ?? "Conflict occurred."));
        }

        // Cualquier otro status (401/403/5xx/status no mapeado) degrada de
        // forma elegante a un resultado tipado en vez de propagar una
        // excepción vía EnsureSuccessStatusCode. Preserva el título/detalle
        // del ProblemDetails cuando el backend lo envió; si no, usa un
        // fallback estable que la UI puede mostrar sin romperse.
        return UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(
                UnidadOrganizativaErrorType.Validation,
                parsed.Title ?? "Unexpected",
                parsed.Detail ?? "Unexpected response status."));
    }

    private static string BuildQueryUri(int page, int pageSize, string? search, string? status = null)
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

        return builder.ToString();
    }
}
