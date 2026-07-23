using Microsoft.Extensions.Logging;
using SGV.Contracts.Habilidades.Categorias.Consultas;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Helper compartido para cargar el catálogo de categorías de habilidad
/// desde los PageModels de Create/Edit. Centraliza la lógica de short-circuit
/// (sentinel <c>null</c> para distinguir "no cargado" de "catálogo vacío"),
/// el catch de transporte vía <see cref="TransportFailureClassifier"/>, y
/// la observabilidad del fallo.
/// </summary>
internal static class CategoriaHabilidadCatalogLoader
{
    /// <summary>
    /// Carga el catálogo de categorías una sola vez por ciclo de vida del
    /// PageModel. Usa <paramref name="current"/> como sentinel: cuando es
    /// <c>null</c> se ejecuta el HTTP call; cuando ya tiene un valor
    /// (aunque sea una lista vacía) se retorna sin llamar al API.
    /// </summary>
    /// <param name="client">Cliente tipado hacia <c>/api/v1/categorias-habilidad</c>.</param>
    /// <param name="logger">Logger del PageModel caller para observabilidad.</param>
    /// <param name="current">Estado actual del catálogo (<c>null</c> = no cargado).</param>
    /// <param name="ct">Token de cancelación cooperativa.</param>
    /// <returns>Tupla con la lista resultante y un flag que indica si el
    /// transporte falló (el PageModel puede usarlo para setear
    /// <c>ErrorMessage</c> sin sobreescribir errores previos).</returns>
    public static async Task<(IReadOnlyList<CategoriaHabilidadDto> Categorias, bool TransportFailed)> LoadAsync(
        ICategoriaHabilidadApiClient client,
        ILogger logger,
        IReadOnlyList<CategoriaHabilidadDto>? current,
        CancellationToken ct)
    {
        if (current is not null)
        {
            return (current, false);
        }

        try
        {
            var categorias = await client.GetAllAsync(ct).ConfigureAwait(false);
            return (categorias, false);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load categorias de habilidad catalog.");
            return ([], true);
        }
    }
}
