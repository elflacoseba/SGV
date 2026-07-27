using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;
using ContractsPuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP que consume los endpoints de puestos de la API.
/// </summary>
/// <remarks>
/// Slice 2 (#125): este cliente ya no mantiene una matriz privada
/// status→categoría. La rama no exitosa delega en
/// <see cref="CommandResultMapper.Map"/>; los records de error de
/// dominio (<see cref="PuestoError"/>, <see cref="PuestoDeleteResult"/>)
/// preservan <c>Categoria</c> poblado por el mapper. Los enums legacy
/// (<see cref="PuestoErrorType"/>) se siguen alimentando vía
/// mapeo a-legacy para mantener source-compat durante el ciclo del change.
/// </remarks>
public sealed class PuestosApiClient(HttpClient httpClient) : IPuestosApiClient
{
    private const string BaseRoute = "/api/v1/puestos";

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<PuestoDto>> QueryAsync(
        ContractsPuestoListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await httpClient.GetAsync(BuildQueryUri(query), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<PuestoDto>>(cancellationToken)
            ?? new PagedResult<PuestoDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<PuestoCommandResult> CreateAsync(
        CrearPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken);
            return PuestoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken);
            return PuestoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);
        var result = await DeleteResultMapper.BuildDeleteResultAsync(
            response,
            HttpStatusCode.NoContent,
            cancellationToken);

        return new PuestoDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    /// <inheritdoc />
    public async Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken);
            return PuestoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    private static string BuildQueryUri(ContractsPuestoListQuery query)
    {
        var builder = new StringBuilder(
            $"{BaseRoute}/consulta?page={query.Page}&pageSize={query.PageSize}");

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

        if (query.Segmento == PuestoSegmentoListado.Eliminadas)
        {
            builder.Append("&status=eliminadas");
        }

        return builder.ToString();
    }

    private static async Task<PuestoCommandResult> ToCommandResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacyType(categoria);
        var error = new PuestoError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return PuestoCommandResult.Failure(error, parsed.FieldErrors);
        }

        return PuestoCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="PuestoErrorType"/>
    /// legacy preservando source-compat: <c>NotFound/Conflict/Validation</c>
    /// son 1-a-1; el resto cae en <see cref="PuestoErrorType.Validation"/>
    /// (no hay variante legacy; se preserva el campo <c>Type</c> no nulo).
    /// </summary>
    private static PuestoErrorType MapCategoriaToLegacyType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => PuestoErrorType.NotFound,
        ErrorCategoria.Conflict => PuestoErrorType.Conflict,
        ErrorCategoria.Validation => PuestoErrorType.Validation,
        ErrorCategoria.Unauthorized => PuestoErrorType.Validation,
        ErrorCategoria.Forbidden => PuestoErrorType.Validation,
        ErrorCategoria.Transport => PuestoErrorType.Validation,
        ErrorCategoria.Unexpected => PuestoErrorType.Validation
    };
}
