using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Modelo de entrada vinculado al formulario Create de Vacante.
/// NO expone <c>EstadoVacanteId</c>: toda vacante nueva arranca en
/// "Abierta" resuelto por la capa de Aplicación
/// (<c>VacanteServicioComandos.CrearAsync</c>).
/// </summary>
/// <remarks>
/// Cambio <c>vacantes-hardening</c> D-3: el split del viejo
/// <c>VacanteInputModel</c> elimina el workaround
/// <c>ModelState.Remove("Input.EstadoVacanteId")</c> que
/// <c>Create.cshtml.cs</c> necesitaba cuando el campo compartía
/// <c>[Required]</c> con Edit.
/// </remarks>
public sealed class VacanteCreateInputModel
{
    /// <summary>Puesto organizacional seleccionado.</summary>
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    /// <summary>Fecha de apertura de la vacante.</summary>
    [Required(ErrorMessage = "La fecha de apertura es obligatoria.")]
    [Display(Name = "Fecha de apertura")]
    [DataType(DataType.Date)]
    public DateTime? FechaApertura { get; set; }

    /// <summary>Motivo de apertura (opcional).</summary>
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    /// <summary>Observaciones libres (opcional).</summary>
    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
