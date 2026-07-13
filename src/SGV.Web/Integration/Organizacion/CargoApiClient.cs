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
/// Cliente HTTP que consume los endpoints de cargos de la API.
/// </summary>
/// <remarks>
/// Slice 2 (#125): este cliente ya no mantiene una matriz privada
/// status→categoría. La rama no exitosa delega en
/// <see cref="CommandResultMapper.Map"/>, única fuente de verdad del
/// shell web. Los records de error de dominio (<see cref="CargoError"/>,
/// <see cref="CargoSkillError"/>) preservan <c>Categoria</c> poblado por
/// el mapper; los enums legacy (<see cref="CargoErrorType"/>,
/// <see cref="CargoSkillErrorType"/>) se siguen alimentando vía los
/// mapeos a-legacy para mantener source-compat durante el ciclo del
/// change.
/// </remarks>
public sealed class CargoApiClient(HttpClient httpClient) : ICargoApiClient
{
    private const string BaseRoute = "/api/v1/cargos";
    private const string NivelesRoute = "/api/v1/niveles-cargo";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CargoDto>>(cancellationToken)
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
        var result = await DeleteResultMapper.BuildDeleteResultAsync(
            response,
            HttpStatusCode.NoContent,
            cancellationToken);

        return new CargoDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken);
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
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(NivelesRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<NivelCargoDto>>(cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Sort, query.Status);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<CargoDto>>(cancellationToken)
            ?? new PagedResult<CargoDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken);
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
        // excepción para que la Razor Page muestre un error recuperable.
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
            // PR3a review follow-up (R1): si el backend responde 2xx con body
            // vacío, ReadFromJsonAsync devuelve null o tira JsonException.
            // Capturamos ambos y devolvemos un Failure tipado Validation/
            // EmptyBody para que la Razor Page muestre el mensaje estándar
            // sin filtrar una excepción nativa al usuario.
            CargoSkillDto? dto;
            try
            {
                dto = await response.Content
                    .ReadFromJsonAsync<CargoSkillDto>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                dto = null;
            }

            if (dto is null)
            {
                return CargoSkillCommandResult.Failure(
                    new CargoSkillError(
                        CargoSkillErrorType.Validation,
                        "EmptyBody",
                        "El servidor respondió 200 sin payload.",
                        Categoria: ErrorCategoria.Validation));
            }

            return CargoSkillCommandResult.Success(dto);
        }

        return await ToSkillCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .DeleteAsync($"{BaseRoute}/{cargoId}/skills/{skillId}", cancellationToken)
            .ConfigureAwait(false);
        var result = await DeleteResultMapper.BuildDeleteResultAsync(
            response,
            HttpStatusCode.NoContent,
            cancellationToken);

        return new CargoSkillDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
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
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacyType(categoria);
        var error = new CargoError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return CargoCommandResult.Failure(error, parsed.FieldErrors);
        }

        return CargoCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="CargoErrorType"/>
    /// legacy preservando source-compat: <c>NotFound/Conflict/Validation</c>
    /// son 1-a-1; el resto (<c>Unauthorized/Forbidden/Transport/Unexpected</c>)
    /// cae en <see cref="CargoErrorType.Validation"/> (no hay variante
    /// legacy; se preserva el campo <c>Type</c> no nulo).
    /// </summary>
    private static CargoErrorType MapCategoriaToLegacyType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => CargoErrorType.NotFound,
        ErrorCategoria.Conflict => CargoErrorType.Conflict,
        ErrorCategoria.Validation => CargoErrorType.Validation,
        ErrorCategoria.Unauthorized => CargoErrorType.Validation,
        ErrorCategoria.Forbidden => CargoErrorType.Validation,
        ErrorCategoria.Transport => CargoErrorType.Validation,
        ErrorCategoria.Unexpected => CargoErrorType.Validation
    };

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="CargoSkillErrorType"/>
    /// legacy. La relación es 1-a-1 salvo para <see cref="ErrorCategoria.Unexpected"/>
    /// que no tiene variante legacy y colapsa a <see cref="CargoSkillErrorType.Validation"/>.
    /// </summary>
    private static CargoSkillErrorType MapCategoriaToLegacySkillType(ErrorCategoria categoria)
    {
        try
        {
            return ErrorCategoriaMappers.ToTipoCargoSkill(categoria);
        }
        catch (NotSupportedException)
        {
            // Unexpected no tiene equivalente en CargoSkillErrorType legacy
            // (sólo cubre NotFound/Validation/Conflict/Unauthorized/Forbidden/Transport).
            return CargoSkillErrorType.Validation;
        }
    }

    /// <summary>
    /// Construye un <see cref="CargoSkillCommandResult"/> a partir de una
    /// respuesta HTTP no exitosa del subrecurso <c>PUT /api/v1/cargos/{cargoId}/skills/{skillId}</c>.
    /// Para 400 con <c>ValidationProblemDetails</c> conserva los FieldErrors;
    /// el resto pasa por el mapper común.
    /// </summary>
    private static async Task<CargoSkillCommandResult> ToSkillCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacySkillType(categoria);
        var error = new CargoSkillError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return CargoSkillCommandResult.Failure(error, parsed.FieldErrors);
        }

        return CargoSkillCommandResult.Failure(error);
    }
}
