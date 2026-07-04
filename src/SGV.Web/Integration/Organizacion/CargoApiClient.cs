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
    public async Task<IReadOnlyList<CargoSkillDetailDto>> GetSkillsAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{cargoId}/skills", cancellationToken);

        // 404 → estado vacío recuperable para la grilla editable de PR3b;
        // cualquier otro status que no sea 2xx sigue propagándose como
        // excepción para que la Razor Page muestre un error recuperable
        // (alineado con el patrón GetByIdAsync, que devuelve null en 404 y
        // deja pasar el resto a EnsureSuccessStatusCode).
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<CargoSkillDetailDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<CargoSkillCommandResult> UpsertSkillAsync(Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PutAsJsonAsync($"{BaseRoute}/{cargoId}/skills/{skillId}", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<CargoSkillDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return CargoSkillCommandResult.Success(dto!);
        }

        return await ToSkillCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .DeleteAsync($"{BaseRoute}/{cargoId}/skills/{skillId}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new CargoSkillDeleteResult(true, response.StatusCode, null, null);
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
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

        return new CargoSkillDeleteResult(
            false,
            response.StatusCode,
            problem?.Title,
            problem?.Detail);
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

    /// <summary>
    /// Traduce una respuesta no exitosa del subrecurso <c>PUT
    /// /api/v1/cargos/{cargoId}/skills/{skillId}</c> a un
    /// <see cref="CargoSkillCommandResult"/>. Bifurca entre
    /// <c>ValidationProblemDetails</c> (cuando el cuerpo trae errores por
    /// campo) y <c>ProblemDetails</c> (fallo plano). Se mantiene deliberadamente
    /// separada de <see cref="ToCommandResultAsync"/> — el subrecurso sólo emite
    /// 400/401/403/404 en la rama de errores, pero el helper se mantiene
    /// defensivo para que un futuro 409 ó 500 también se traduzca a un
    /// <see cref="CargoSkillErrorType"/> sin propagar la excepción cruda al
    /// consumidor.
    /// </summary>
    private static async Task<CargoSkillCommandResult> ToSkillCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (validation?.Errors is { Count: > 0 })
            {
                var fieldErrors = validation.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return CargoSkillCommandResult.Failure(
                    new CargoSkillError(
                        CargoSkillErrorType.Validation,
                        validation.Title ?? "DatosInvalidos",
                        validation.Detail ?? "Uno o más campos del vínculo contienen errores de validación."),
                    fieldErrors);
            }

            return CargoSkillCommandResult.Failure(
                new CargoSkillError(
                    CargoSkillErrorType.Validation,
                    validation?.Title ?? "BadRequest",
                    validation?.Detail ?? "Solicitud inválida."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return CargoSkillCommandResult.Failure(
                new CargoSkillError(
                    CargoSkillErrorType.NotFound,
                    problem?.Title ?? "NotFound",
                    problem?.Detail ?? "Recurso no encontrado."));
        }

        // El subrecurso no emite 409 hoy; el helper se mantiene defensivo por
        // simetría con ToCommandResultAsync y para que un cambio futuro del
        // backend no devuelva una excepción cruda al cliente web.
        return CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "Unexpected",
                "Respuesta inesperada del servidor."));
    }
}
