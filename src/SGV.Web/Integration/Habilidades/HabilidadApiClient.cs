using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Cliente HTTP que consume los endpoints de habilidades de la API.
/// </summary>
public sealed class HabilidadApiClient(
    HttpClient httpClient,
    ILogger<HabilidadApiClient> logger) : IHabilidadApiClient
{
    private const string BaseRoute = "/api/v1/skills";
    private const string NivelesRoute = "/api/v1/niveles-habilidad";

    /// <inheritdoc />
    public async Task<IReadOnlyList<HabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<HabilidadDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HabilidadDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HabilidadDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);
        var result = await DeleteResultMapper.BuildDeleteResultAsync(
            response,
            HttpStatusCode.NoContent,
            cancellationToken);

        return new HabilidadDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    /// <inheritdoc />
    public async Task<HabilidadCommandResult> CreateAsync(
        CrearHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<HabilidadDto>(cancellationToken: cancellationToken);
            return HabilidadCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HabilidadCommandResult> UpdateAsync(
        Guid id,
        ActualizarHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<HabilidadDto>(cancellationToken: cancellationToken);
            return HabilidadCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NivelHabilidadDto>> GetNivelesHabilidadAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(NivelesRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<NivelHabilidadDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PagedResult<HabilidadDto>> QueryAsync(
        HabilidadListQuery query,
        CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Sort, query.Status);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<HabilidadDto>>(cancellationToken: cancellationToken)
            ?? new PagedResult<HabilidadDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<HabilidadCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<HabilidadDto>(cancellationToken: cancellationToken);
            return HabilidadCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SkillCargoDetailDto>> GetCargosAsync(
        Guid skillId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default)
    {
        var segmentoText = query.Segmento == HabilidadSegmentoListado.Eliminadas ? "eliminadas" : null;
        var requestUri = BuildCargosUri(skillId, query.Page, query.PageSize, query.Search, query.Sort, segmentoText);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<SkillCargoDetailDto>>(cancellationToken: cancellationToken)
            ?? new PagedResult<SkillCargoDetailDto>([], 0, query.Page, query.PageSize);
    }

    private static string BuildCargosUri(
        Guid skillId,
        int page,
        int pageSize,
        string? search,
        string? sort,
        string? status)
    {
        var builder = new StringBuilder($"{BaseRoute}/{skillId}/cargos?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(sort));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.Append("&status=");
            builder.Append(Uri.EscapeDataString(status));
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public async Task<PersonaHabilidadesPageResult> GetPersonasAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default)
    {
        var segmentoText = query.Segmento == PersonaSegmentoListado.Eliminadas ? "eliminadas" : null;
        var requestUri = BuildPersonasUri(skillId, query.Page, query.PageSize, query.Search, query.Sort, segmentoText);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PersonaHabilidadesPageResult>(cancellationToken: cancellationToken)
            ?? new PersonaHabilidadesPageResult(
                [],
                Page: query.Page,
                PageSize: query.PageSize,
                Total: 0,
                Sort: query.Sort,
                Segmento: query.Segmento);
    }

    private static string BuildPersonasUri(
        Guid skillId,
        int page,
        int pageSize,
        string? search,
        string? sort,
        string? status)
    {
        var builder = new StringBuilder($"{BaseRoute}/{skillId}/personas?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(sort));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.Append("&status=");
            builder.Append(Uri.EscapeDataString(status));
        }

        return builder.ToString();
    }

    private static string BuildQueryUri(int page, int pageSize, string? search, string? sort = null, string? status = null)
    {
        var builder = new StringBuilder($"{BaseRoute}/consulta?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(sort));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.Append("&status=");
            builder.Append(Uri.EscapeDataString(status));
        }

        return builder.ToString();
    }

    private async Task<HabilidadCommandResult> ToCommandResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        // Mantenemos observabilidad para errores no esperados / Transporte:
        // el operador que lee logs debe ver el status crudo con método y URI.
        if (categoria == ErrorCategoria.Unexpected || categoria == ErrorCategoria.Transport)
        {
            logger.LogError(
                "HabilidadApiClient received {Categoria} status {StatusCode} on {Method} {Uri}.",
                categoria,
                statusCode,
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri);
        }

        // El backend usa el código CategoriaHabilidadNoExiste cuando el
        // CategoriaId informado no está en el catálogo seed. Lo traducimos
        // a CategoriaInexistente para que el PageModel pueda ramificar por
        // HabilidadErrorType.
        HabilidadErrorType errorType;
        if (string.Equals(code, "CategoriaHabilidadNoExiste", StringComparison.OrdinalIgnoreCase))
        {
            errorType = HabilidadErrorType.CategoriaInexistente;
        }
        else
        {
            errorType = MapCategoriaToType(categoria);
        }

        var error = new HabilidadError(errorType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return HabilidadCommandResult.Failure(error, parsed.FieldErrors);
        }

        return HabilidadCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="HabilidadErrorType"/>
    /// vigente preservando source-compat:
    /// <list type="bullet">
    ///   <item><description><c>NotFound</c> → <see cref="HabilidadErrorType.NotFound"/></description></item>
    ///   <item><description><c>Conflict</c> → <see cref="HabilidadErrorType.Conflict"/></description></item>
    ///   <item><description><c>Validation</c> → <see cref="HabilidadErrorType.Validation"/></description></item>
    ///   <item><description><c>Transport</c> → <see cref="HabilidadErrorType.Infrastructure"/></description></item>
    ///   <item><description><c>Unauthorized</c>, <c>Forbidden</c>, <c>Unexpected</c> caen en <see cref="HabilidadErrorType.Validation"/> (no hay variante legacy; se preserva el campo <c>Type</c> no nulo para no romper callers que ramifican por el enum).</description></item>
    /// </list>
    /// La semántica completa vive en <see cref="ErrorCategoria"/>; los
    /// callers nuevos deben ramificar por <c>Categoria</c>.
    /// </summary>
    private static HabilidadErrorType MapCategoriaToType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => HabilidadErrorType.NotFound,
        ErrorCategoria.Conflict => HabilidadErrorType.Conflict,
        ErrorCategoria.Validation => HabilidadErrorType.Validation,
        ErrorCategoria.Transport => HabilidadErrorType.Infrastructure,
        ErrorCategoria.Unauthorized => HabilidadErrorType.Validation,
        ErrorCategoria.Forbidden => HabilidadErrorType.Validation,
        _ => HabilidadErrorType.Validation
    };
}