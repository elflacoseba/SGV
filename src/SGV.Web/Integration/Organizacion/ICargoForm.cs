using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

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
    /// Indicates whether the page is rendering in edit mode. Unused until
    /// PR2B (Edit page) sets it to <c>true</c>; it is introduced now so the
    /// shared partial contract (<c>_Form.cshtml</c>) does not change between
    /// PR2A and PR2B. The Create implementation always returns
    /// <c>false</c>; the Edit implementation will return <c>true</c> and may
    /// use it to adjust the page title, the submit button label, or to
    /// suppress read-only fields.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>
    /// URL de retorno al listado preservando filtros de la página anterior.
    /// </summary>
    string ReturnToListUrl { get; }
}
