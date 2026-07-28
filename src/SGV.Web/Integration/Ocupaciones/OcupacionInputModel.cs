using System.ComponentModel.DataAnnotations;
using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Input model para el formulario create/edit de una Ocupación. Reused by
/// <c>Create.cshtml</c> y <c>Edit.cshtml</c> del módulo web de Ocupaciones
/// (Slice 3a del change <c>2026-07-28-web-ocupaciones-issue-208</c>).
/// La validación cliente+servidor se aplica vía <see cref="ValidationAttribute"/>
/// declarativos; el PageModel consume los valores con <c>BindProperty</c> y
/// delega el chequeo final a <c>ModelState.IsValid</c>.
/// </summary>
/// <remarks>
/// Los tipos son <see cref="Nullable{T}"/> para que <see cref="RequiredAttribute"/>
/// pueda fallar en POST inválido (los value types no-nullable siempre pasan
/// <c>[Required]</c> por construcción). El PageModel los desreferencia con
/// <c>!</c> tras <c>ModelState.IsValid</c>.
/// </remarks>
public sealed class OcupacionInputModel
{
    /// <summary>Identificador de la persona a la que se asigna la ocupación.</summary>
    [Required(ErrorMessage = "Debe escoger una persona.")]
    [Display(Name = "Persona")]
    public Guid? PersonaId { get; set; }

    /// <summary>Identificador del puesto que ocupa la persona.</summary>
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    /// <summary>Fecha de inicio de la ocupación (formato <c>yyyy-MM-dd</c>).</summary>
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [Display(Name = "Fecha de inicio")]
    public DateOnly? FechaInicio { get; set; }

    /// <summary>Tipo de asignación (permanente, interino, temporal).</summary>
    [Required(ErrorMessage = "Debe escoger el tipo de asignación.")]
    [Display(Name = "Tipo de asignación")]
    public OcupacionTipoAsignacion? TipoAsignacion { get; set; }

    /// <summary>Observaciones opcionales (máximo 500 caracteres).</summary>
    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}