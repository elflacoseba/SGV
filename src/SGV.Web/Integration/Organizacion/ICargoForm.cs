using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de cargos, tanto para creación como edición.
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
    /// Indicates whether the page is rendering in edit mode. The Edit
    /// implementation sets it to <c>true</c> so the shared partial contract
    /// (<c>_Form.cshtml</c>) can adjust the page title, the submit button
    /// label, or suppress read-only fields. The Create implementation always
    /// returns <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>
    /// URL de retorno al listado preservando filtros de la página anterior.
    /// </summary>
    string ReturnToListUrl { get; }
}
