using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Typed HTTP client for unidades organizativas endpoints.
/// </summary>
public sealed class UnidadOrganizativaApiClient(HttpClient httpClient) : IUnidadOrganizativaApiClient
{
    private const string BaseRoute = "/api/v1/unidades-organizativas";

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
