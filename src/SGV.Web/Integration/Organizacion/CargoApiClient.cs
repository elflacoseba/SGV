using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP que consume los endpoints de cargos de la API.
/// </summary>
public sealed class CargoApiClient(HttpClient httpClient) : ICargoApiClient
{
    private const string BaseRoute = "/api/v1/cargos";
    private const string NivelesRoute = "/api/v1/niveles-cargo";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CargoDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new CargoDeleteResult(true, response.StatusCode, null, null);
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

        return new CargoDeleteResult(
            false,
            response.StatusCode,
            problem?.Title,
            problem?.Detail);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(NivelesRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<NivelCargoDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Sort, query.Status);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<CargoDto>>(cancellationToken: cancellationToken)
            ?? new PagedResult<CargoDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CargoSkillDetailDto>> GetSkillsAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        // Stub temporal durante el ciclo RED del strict TDD: T3.2 reemplaza este
        // cuerpo por la implementación real (mirroring del patrón GetByIdAsync:
        // 2xx → lista parseada, 404 → lista vacía, otros status → throw vía
        // EnsureSuccessStatusCode).
        throw new NotImplementedException("T3.2 placeholder: get skills subresource");
    }

    /// <inheritdoc />
    public Task<CargoSkillCommandResult> UpsertSkillAsync(Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default)
    {
        // Stub temporal durante el ciclo RED del strict TDD: T3.2 reemplaza este
        // cuerpo por la implementación real, incluyendo el helper dedicado
        // ToSkillCommandResultAsync que bifurca ValidationProblemDetails y
        // ProblemDetails manteniendo el contrato de CargoSkillCommandResult.
        throw new NotImplementedException("T3.2 placeholder: upsert skill subresource");
    }

    /// <inheritdoc />
    public Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
    {
        // Stub temporal durante el ciclo RED del strict TDD: T3.2 reemplaza este
        // cuerpo por la implementación real, traduciendo 204 → Success y los
        // no-success a un Failure con ProblemDetails cuando esté disponible.
        throw new NotImplementedException("T3.2 placeholder: delete skill subresource");
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

    private static async Task<CargoCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken);
            if (problem?.Errors is { Count: > 0 })
            {
                var fieldErrors = problem.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Validation, problem.Title ?? "ValidationError", problem.Detail ?? "Uno o más campos son inválidos."),
                    fieldErrors);
            }

            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.Validation, problem?.Title ?? "BadRequest", problem?.Detail ?? "Solicitud inválida."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.NotFound, problem?.Title ?? "NotFound", problem?.Detail ?? "Recurso no encontrado."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.Conflict, problem?.Title ?? "Conflict", problem?.Detail ?? "Conflicto."));
        }

        return CargoCommandResult.Failure(
            new CargoError(CargoErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor."));
    }
}
