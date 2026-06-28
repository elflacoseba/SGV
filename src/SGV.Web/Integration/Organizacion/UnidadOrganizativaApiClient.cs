using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

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
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search);
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
    public async Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new UnidadOrganizativaDeleteResult(true, response.StatusCode, null, null);
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
        }
        catch (NotSupportedException)
        {
        }
        catch (HttpRequestException)
        {
        }

        return new UnidadOrganizativaDeleteResult(
            false,
            response.StatusCode,
            problem?.Title,
            problem?.Detail);
    }

    private static async Task<UnidadOrganizativaCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken);
            if (problem?.Errors is { Count: > 0 })
            {
                var fieldErrors = problem.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, problem.Title ?? "ValidationError", problem.Detail ?? "One or more fields are invalid."),
                    fieldErrors);
            }

            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, problem?.Title ?? "BadRequest", problem?.Detail ?? "Invalid request."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, problem?.Title ?? "NotFound", problem?.Detail ?? "Resource not found."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return UnidadOrganizativaCommandResult.Failure(
                new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, problem?.Title ?? "Conflict", problem?.Detail ?? "Conflict occurred."));
        }

        response.EnsureSuccessStatusCode();
        return UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "Unexpected", "Unexpected response status."));
    }

    private static string BuildQueryUri(int page, int pageSize, string? search)
    {
        var builder = new StringBuilder($"{BaseRoute}/consulta?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        return builder.ToString();
    }
}
