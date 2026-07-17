namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Usuarios (introducido en PR 4 del change
/// <c>Implementa módulo usuarios</c>). <c>Create</c> implementará esta
/// interfaz con <see cref="IsEdit"/> en <c>false</c>; <c>Edit</c> la
/// implementará con <c>true</c>.
/// <para>
/// El partial usa este contrato para compartir el selector modal de Persona
/// entre Create/Edit y ocultar únicamente Password en Edit. El catálogo de
/// roles siempre se renderiza como checkboxes del catálogo fijo
/// <see cref="SGV.Contracts.Seguridad.RolesSgv.Todos"/>.
/// </para>
/// </summary>
public interface IUsuarioForm
{
    /// <summary>Estado del formulario bindable.</summary>
    UsuarioInputModel Input { get; }

    /// <summary>
    /// Texto visible de la Persona seleccionada. Create lo conserva como
    /// campo bindeable para re-renderizar la card tras un error; Edit lo
    /// deriva del DTO del usuario.
    /// </summary>
    string? PersonaDisplay { get; }

    /// <summary>
    /// Mensaje de error general recuperable (consulta caída, error de
    /// transporte en POST, etc.). El partial lo muestra bajo el
    /// <c>asp-validation-summary="ModelOnly"</c>.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit — el partial usa este flag
    /// para ocultar Password. Create siempre devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>URL de retorno al listado preservando los filtros de la página anterior.</summary>
    string ReturnToListUrl { get; }
}