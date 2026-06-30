using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP que consume los endpoints de cargos de la API.
/// </summary>
public sealed class CargoApiClient(HttpClient httpClient) : ICargoApiClient
{
    private const string BaseRoute = "/api/v1/cargos";

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
}