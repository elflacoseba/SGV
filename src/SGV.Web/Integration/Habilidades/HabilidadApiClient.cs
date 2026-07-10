using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;

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

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new HabilidadDeleteResult(true, response.StatusCode, null, null);
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
        catch (System.Text.Json.JsonException)
        {
        }

        return new HabilidadDeleteResult(
            false,
            response.StatusCode,
            problem?.Title,
            problem?.Detail);
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
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken);
            if (problem?.Errors is { Count: > 0 })
            {
                var fieldErrors = problem.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.Validation, problem.Title ?? "ValidationError", problem.Detail ?? "Uno o más campos son inválidos."),
                    fieldErrors);
            }

            return HabilidadCommandResult.Failure(
                new HabilidadError(HabilidadErrorType.Validation, problem?.Title ?? "BadRequest", problem?.Detail ?? "Solicitud inválida."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await TryReadProblemDetailsAsync(response, cancellationToken).ConfigureAwait(false);
            return HabilidadCommandResult.Failure(
                new HabilidadError(HabilidadErrorType.NotFound, problem?.Title ?? "NotFound", problem?.Detail ?? "Recurso no encontrado."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await TryReadProblemDetailsAsync(response, cancellationToken).ConfigureAwait(false);
            return HabilidadCommandResult.Failure(
                new HabilidadError(HabilidadErrorType.Conflict, problem?.Title ?? "Conflict", problem?.Detail ?? "Conflicto."));
        }

        // Status inesperado (5xx, 408, 3xx que cuele, etc.): no lo enmascaremos
        // como Validation. Loggeamos el status real con la respuesta y
        // devolvemos Infrastructure preservando el status code para
        // diagnóstico downstream (la página lo usa para mostrar error de
        // servidor sin asociarlo a un campo del form).
        var statusCode = (int)response.StatusCode;
        logger.LogError(
            "HabilidadApiClient received unexpected status {StatusCode} on {Method} {Uri}.",
            statusCode,
            response.RequestMessage?.Method,
            response.RequestMessage?.RequestUri);
        return HabilidadCommandResult.Failure(new HabilidadError(
            HabilidadErrorType.Infrastructure,
            "ServerError",
            "El servicio de habilidades no respondió correctamente. Intentá nuevamente.",
            StatusCode: statusCode));
    }

    /// <summary>
    /// Lee un <see cref="ProblemDetails"/> del cuerpo de la respuesta,
    /// tolerando cuerpos no-JSON o ausentes (devuelve <c>null</c> en ese
    /// caso en vez de propagar <see cref="System.Text.Json.JsonException"/>).
    /// </summary>
    private static async Task<ProblemDetails?> TryReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}