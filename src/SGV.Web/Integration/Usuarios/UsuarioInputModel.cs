using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Input model para los formularios <c>Create.cshtml</c> y
/// <c>Edit.cshtml</c> del módulo Usuarios (PR 4). Refleja los campos del
/// <c>CrearUsuarioRequest</c>/<c>ActualizarUsuarioRequest</c> de
/// <c>SGV.Contracts</c>; las validaciones DataAnnotations vigentes son
/// las del backend, pero el cliente las repite aquí como defensa de UX
/// (cliente nunca enviaría 400 por campos vacíos obvios). El backend
/// sigue siendo la fuente de verdad.
/// </summary>
/// <remarks>
/// <para>
/// El modelo se usa en PR 4 (Create/Edit). PR 2 sólo define el shape
/// para que el cliente tipado y los tests puedan triangular el binding
/// sin requerir aún las Razor Pages.
/// </para>
/// <para>
/// Regla de catálogo de roles (REQ-UCE-07 del spec): la lista de roles
/// del input se sanea contra <see cref="RolesSgv.Todos"/> via
/// <see cref="IsValidRole"/> antes de enviar al backend. Roles no
/// listados en <see cref="RolesSgv.Todos"/> no se persisten.
/// </para>
/// </remarks>
public sealed class UsuarioInputModel
{
    /// <summary>
    /// Identificador de la persona vinculada (alta solamente). Vacío
    /// en <c>Edit.cshtml</c> (read-only).
    /// </summary>
    [Required(ErrorMessage = "Debe seleccionar una persona activa.")]
    [Display(Name = "Persona")]
    public Guid? PersonaId { get; set; }

    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(50, ErrorMessage = "El nombre de usuario no puede superar los 50 caracteres.")]
    [RegularExpression(
        "^[A-Za-z0-9._-]+$",
        ErrorMessage = "El nombre de usuario sólo admite letras, números, punto, guión bajo y guión medio.")]
    [Display(Name = "Nombre de usuario")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [StringLength(200, ErrorMessage = "El email no puede superar los 200 caracteres.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña inicial. Sólo obligatoria en <c>Create</c>; en
    /// <c>Edit</c> queda vacía (la política de cambio de contraseña
    /// desde admin queda fuera de scope del change). El PageModel
    /// detecta el modo y ajusta el validator antes del POST.
    /// </summary>
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres.")]
    [Display(Name = "Contraseña inicial")]
    public string? Password { get; set; }

    /// <summary>
    /// Lista bindeable de roles. El PageModel sanitiza contra
    /// <see cref="RolesSgv.Todos"/> antes de enviar al cliente.
    /// </summary>
    [Required(ErrorMessage = "Debe asignar al menos un rol.")]
    [MinLength(1, ErrorMessage = "Debe asignar al menos un rol.")]
    [Display(Name = "Roles")]
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Catálogo auxiliar visible para la grilla de checkboxes (no se
    /// serializa al backend; lo expone <c>RolesSgv.Todos</c>).
    /// </summary>
    public IReadOnlyList<string> RolesCatalogo => RolesSgv.Todos;

    /// <summary>
    /// Es <c>true</c> cuando se renderiza el editor; usado por
    /// <c>_Form.cshtml</c> para mostrar/ocultar campos sensibles al
    /// modo (Persona sólo Create, Password sólo Create).
    /// </summary>
    public bool IsEdit { get; set; }

    /// <summary>
    /// Predicate de defensa que filtra la lista bindeable contra el
    /// catálogo <see cref="RolesSgv.Todos"/>. La página lo invoca antes
    /// del POST para evitar enviar roles no vigentes (e.g. defaults de
    /// Identity como <c>"User"</c>).
    /// </summary>
    public static IReadOnlyList<string> FilterByCatalog(IEnumerable<string> roles)
        => roles.Where(RolesSgv.EsValido).ToArray();

    /// <summary>
    /// True si todos los roles del input están en el catálogo fijo.
    /// </summary>
    public bool RolesAreValid()
        => Roles.All(RolesSgv.EsValido);
}
