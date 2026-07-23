using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de habilidades (Create/Edit). NO incluye catálogo
/// de niveles porque <c>Habilidad</c> no modela nivel propio en el
/// catálogo maestro.
/// </summary>
public interface IHabilidadForm
{
    /// <summary>
    /// Estado del formulario bindable.
    /// </summary>
    HabilidadInputModel Input { get; }

    /// <summary>
    /// Mensaje de error general recuperable (catálogo caído, etc.).
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando se renderiza en edit; <c>false</c> en create.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>
    /// URL de retorno al listado preservando filtros.
    /// </summary>
    string ReturnToListUrl { get; }

    /// <summary>
    /// Catálogo de categorías de habilidad disponible para poblar el &lt;select&gt;
    /// de categoría en el formulario.
    /// </summary>
    IReadOnlyList<CategoriaHabilidadDto> CategoriasDisponibles { get; }
}
