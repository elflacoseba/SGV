using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Bound input for creating or editing a vacante.
/// </summary>
public sealed class VacanteInputModel
{
    /// <summary>Selected organizational position.</summary>
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    /// <summary>Selected vacancy state.</summary>
    [Required(ErrorMessage = "Debe escoger un estado.")]
    [Display(Name = "Estado")]
    public Guid? EstadoVacanteId { get; set; }

    /// <summary>Opening date of the vacancy.</summary>
    [Required(ErrorMessage = "La fecha de apertura es obligatoria.")]
    [Display(Name = "Fecha de apertura")]
    [DataType(DataType.Date)]
    public DateTime? FechaApertura { get; set; }

    /// <summary>Reason for opening or closing the vacancy.</summary>
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    /// <summary>Optional free-form observations.</summary>
    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
