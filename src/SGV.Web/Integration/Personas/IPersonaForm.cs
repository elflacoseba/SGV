using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Personas. Create/Edit exponen el catálogo de
/// tipos de documento (vía <see cref="IPersonaApiClient.GetTiposDocumentoAsync"/>)
/// como <see cref="TiposDocumento"/> para que la vista lo proyecte a un
/// <c>&lt;select&gt;</c>. La fuente de verdad wire del catálogo vive en
/// <c>SGV.Contracts/Personas/Consultas/Dtos/TipoDocumentoDto.cs</c> (issue #147).
/// </summary>
public interface IPersonaForm
{
    /// <summary>Estado del formulario bindable.</summary>
    PersonaInputModel Input { get; }

    /// <summary>
    /// Catálogo de tipos de documento cargado por el PageModel. Se
    /// materializa como <c>SelectList</c> en la vista con
    /// <c>asp-items="Model.TiposDocumentoSelectList"</c>. Si el catálogo
    /// cae (transport failure), la vista sigue renderizando el
    /// placeholder "Seleccionar tipo…" y muestra un mensaje de error
    /// recuperable.
    /// </summary>
    IReadOnlyList<TipoDocumentoDto> TiposDocumento { get; }

    /// <summary>
    /// Mensaje de error general recuperable (catálogo caído, error de
    /// transporte en POST, etc.). El partial lo muestra bajo el
    /// <c>asp-validation-summary="ModelOnly"</c>.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit — el partial usa este
    /// flag para diferenciar el botón "Guardar" / "Actualizar" y para
    /// informar al usuario de la operación en curso. Create siempre
    /// devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>
    /// URL de retorno al listado preservando los filtros de la página anterior.</summary>
    string ReturnToListUrl { get; }
}
