using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Usuarios (introducido en PR 4 del change
/// <c>Implementa módulo usuarios</c>). <c>Create</c> implementará esta
/// interfaz con <see cref="IsEdit"/> en <c>false</c>; <c>Edit</c> la
/// implementará con <c>true</c>.
/// <para>
/// El partial usa este contrato para renderizar distinto según el modo:
/// el dropdown de Personas y el input de Password sólo aparecen en
/// Create (la Persona es inmutable en Edit; el cambio de password queda
/// fuera del scope del change). El catálogo de roles siempre se
/// renderiza como checkboxes del catálogo fijo
/// <see cref="SGV.Contracts.Seguridad.RolesSgv.Todos"/>.
/// </para>
/// </summary>
public interface IUsuarioForm
{
    /// <summary>Estado del formulario bindable.</summary>
    UsuarioInputModel Input { get; }

    /// <summary>
    /// Opciones del catálogo de Personas activas para popular el dropdown
    /// de <c>PersonaId</c>. Sólo se renderiza en Create
    /// (<see cref="IsEdit"/> es <c>false</c>); en Edit la Persona es
    /// inmutable y se muestra como read-only.
    /// </summary>
    IReadOnlyList<PersonaDto> PersonaOptions { get; }

    /// <summary>
    /// Mensaje de error general recuperable (catálogo caído, error de
    /// transporte en POST, etc.). El partial lo muestra bajo el
    /// <c>asp-validation-summary="ModelOnly"</c>.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit — el partial usa este flag
    /// para ocultar los campos inmutables (Persona dropdown + Password).
    /// Create siempre devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>URL de retorno al listado preservando los filtros de la página anterior.</summary>
    string ReturnToListUrl { get; }
}