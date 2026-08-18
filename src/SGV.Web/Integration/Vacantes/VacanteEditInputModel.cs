using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Modelo de entrada vinculado al formulario Edit de Vacante.
/// Incluye <c>EstadoVacanteId</c> con <c>[Required]</c> porque Edit
/// permite transiciones de estado explícitas; Create no.
/// </summary>
/// <remarks>
/// Cambio <c>vacantes-hardening</c> D-3: el split del viejo
/// <c>VacanteInputModel</c> elimina el workaround
/// <c>ModelState.Remove("Input.EstadoVacanteId")</c> que
/// <c>Create.cshtml.cs</c> necesitaba cuando el campo compartía
/// <c>[Required]</c> con Edit.
/// </remarks>
public sealed class VacanteEditInputModel
{
    /// <summary>Puesto organizacional (read-only vía hidden input).</summary>
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    /// <summary>Estado destino de la transición.</summary>
    [Required(ErrorMessage = "Debe escoger un estado.")]
    [Display(Name = "Estado")]
    public Guid? EstadoVacanteId { get; set; }

    /// <summary>Fecha de apertura de la vacante (read-only).</summary>
    [Required(ErrorMessage = "La fecha de apertura es obligatoria.")]
    [Display(Name = "Fecha de apertura")]
    [DataType(DataType.Date)]
    public DateTime? FechaApertura { get; set; }

    /// <summary>Motivo de la transición (opcional).</summary>
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    /// <summary>Observaciones libres (opcional).</summary>
    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
