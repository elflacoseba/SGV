using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Input model for the create/edit form of an organizational unit.
/// </summary>
public sealed class UnidadOrganizativaInputModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [Display(Name = "Vigente desde")]
    public DateOnly? VigenteDesde { get; set; }

    [Display(Name = "Vigente hasta")]
    public DateOnly? VigenteHasta { get; set; }

    [Required(ErrorMessage = "El tipo es obligatorio.")]
    [Display(Name = "Tipo")]
    public Guid TipoUnidadOrganizativaId { get; set; }

    [Display(Name = "Unidad padre")]
    public Guid? UnidadPadreId { get; set; }
}
