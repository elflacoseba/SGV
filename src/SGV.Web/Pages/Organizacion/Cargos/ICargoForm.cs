using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de cargos (Create en PR2A, Edit en PR2B).
/// </summary>
public interface ICargoForm
{
    /// <summary>
    /// Estado del formulario bindable.
    /// </summary>
    CargoInputModel Input { get; }

    /// <summary>
    /// Opciones del catálogo de niveles para popular el dropdown.
    /// </summary>
    IReadOnlyList<NivelCargoDto> NivelOptions { get; }

    /// <summary>
    /// Mensaje de error general recuperable (catálogo caído, etc.).
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Indica si la página renderiza en modo edición. PR2A siempre
    /// devuelve <c>false</c> (Create); PR2B lo activará desde Edit.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>
    /// URL de retorno al listado preservando filtros de la página anterior.
    /// </summary>
    string ReturnToListUrl { get; }
}
