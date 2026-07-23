using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Implementación HTTP tipada del cliente de catálogo de categorías de
/// habilidad. Consume los endpoints GET read-only del controlador
/// <c>CategoriasHabilidadController</c> vía el pipeline autenticado
/// <c>ApiBearerTokenHandler</c>.
/// </summary>
public sealed class CategoriaHabilidadApiClient(HttpClient httpClient) : ICategoriaHabilidadApiClient
{
    private const string BaseRoute = "/api/v1/categorias-habilidad";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoriaHabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // El diseño web-apiclient-transport-contract exige que un token
        // pre-cancelado no inicie el envío HTTP.
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.GetAsync(BaseRoute, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<CategoriaHabilidadDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    /// <inheritdoc />
    public async Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<CategoriaHabilidadDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
