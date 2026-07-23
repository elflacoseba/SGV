using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Cliente HTTP tipado de sólo-lectura para el catálogo inmutable
/// <c>CategoriasHabilidad</c>. Expone operaciones GET hacia los endpoints
/// <c>/api/v1/categorias-habilidad</c> (listar) y
/// <c>/api/v1/categorias-habilidad/{id}</c> (obtener por id). No expone
/// operaciones de escritura porque el catálogo es seed-only.
/// </summary>
public interface ICategoriaHabilidadApiClient
{
    /// <summary>
    /// Lista todas las categorías de habilidad disponibles en el catálogo.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <returns>Colección de categorías. Vacía cuando el catálogo está en
    /// estado inicial o la API responde con colección vacía.</returns>
    /// <exception cref="HttpRequestException">Errores de transporte
    /// (DNS, conexión rechazada, 5xx). El consumidor debe decidir si la
    /// trata como error recuperable.</exception>
    /// <exception cref="TaskCanceledException">Timeout o cancelación
    /// del token. El consumidor debe verificar
    /// <c>cancellationToken.IsCancellationRequested</c> para distinguir
    /// timeout (property de HttpClient) de cancelación cooperativa.</exception>
    Task<IReadOnlyList<CategoriaHabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una categoría por su identificador o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador de la categoría en el catálogo.</param>
    /// <param name="cancellationToken">Token de cancelación cooperativa.</param>
    /// <returns>La categoría o <c>null</c> cuando la API responde 404.</returns>
    /// <exception cref="HttpRequestException">Errores de transporte.</exception>
    /// <exception cref="TaskCanceledException">Timeout o cancelación.</exception>
    Task<CategoriaHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
