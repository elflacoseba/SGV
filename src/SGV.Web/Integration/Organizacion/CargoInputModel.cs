using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Input model for the create/edit form of a Cargo.
/// </summary>
public sealed class CargoInputModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "El nivel es obligatorio.")]
    [Display(Name = "Nivel")]
    public Guid NivelId { get; set; }
}
