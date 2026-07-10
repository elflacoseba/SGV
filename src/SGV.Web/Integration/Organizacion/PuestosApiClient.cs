using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP que consume los endpoints de puestos de la API.
/// </summary>
public sealed class PuestosApiClient(HttpClient httpClient) : IPuestosApiClient
{
    private const string BaseRoute = "/api/v1/puestos";

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PuestoDto>>(cancellationToken: cancellationToken)
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
    public async Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: cancellationToken);
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
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: cancellationToken);
            return PuestoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new PuestoDeleteResult(true, response.StatusCode, null, null);
        }

        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);

        return new PuestoDeleteResult(
            false,
            response.StatusCode,
            parsed.Title,
            parsed.Detail);
    }

    /// <inheritdoc />
    public async Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<PuestoDto>(cancellationToken: cancellationToken);
            return PuestoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <summary>
    /// Traduce respuestas no exitosas a <see cref="PuestoCommandResult.Failure(PuestoError)"/>.
    /// Para 400 bifurca entre <c>ValidationProblemDetails</c> (errores por campo)
    /// y <c>ProblemDetails</c> plano. 404/409 caen en Failure con Code/Message
    /// del <c>ProblemDetails</c>. Es el espejo de
    /// <c>CargoApiClient.ToCommandResultAsync</c>, ajustado al shape del backend
    /// de Puestos.
    /// </summary>
    private static async Task<PuestoCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader.ReadAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            if (parsed.FieldErrors is { Count: > 0 })
            {
                return PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Validation, parsed.Title ?? "DatosInvalidos", parsed.Detail ?? "Uno o más campos son inválidos."),
                    parsed.FieldErrors);
            }

            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Validation, parsed.Title ?? "BadRequest", parsed.Detail ?? "Solicitud inválida."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.NotFound, parsed.Title ?? "PuestoNoEncontrado", parsed.Detail ?? "Recurso no encontrado."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Conflict, parsed.Title ?? "Conflict", parsed.Detail ?? "Conflicto."));
        }

        return PuestoCommandResult.Failure(
            new PuestoError(PuestoErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor."));
    }
}
