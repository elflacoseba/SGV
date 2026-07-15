namespace SGV.Web.Integration.Personas;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Personas (introducido en PR #2 para que el
/// shared contract esté disponible cuando llegue Create/Edit en PR #3).
/// Create (PR 3) implementará esta interfaz con <see cref="IsEdit"/> en
/// <c>false</c>; Edit (PR 3) la implementará con <c>true</c>.
/// </summary>
public interface IPersonaForm
{
    /// <summary>Estado del formulario bindable.</summary>
    PersonaInputModel Input { get; }

    /// <summary>
    /// Mensaje de error general recuperable (catálogo caído, error de
    /// transporte en POST, etc.). El partial lo muestra bajo el
    /// <c>asp-validation-summary="ModelOnly"</c>.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit (PR 3) — el partial usa este
    /// flag para diferenciar el botón "Guardar" / "Actualizar" y para
    /// informar al usuario de la operación en curso. Create siempre
    /// devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>URL de retorno al listado preservando los filtros de la página anterior.</summary>
    string ReturnToListUrl { get; }
}
