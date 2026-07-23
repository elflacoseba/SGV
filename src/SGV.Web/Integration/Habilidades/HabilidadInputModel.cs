using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Input model for the create/edit form of a Habilidad.
/// Replica las validaciones de la entidad de dominio (longitudes) y NO
/// incluye <c>NivelId</c> porque el catálogo maestro de <c>Habilidad</c>
/// no modela nivel propio (el nivel vive en la asociación con cargo o persona).
/// </summary>
public sealed class HabilidadInputModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public Guid? CategoriaId { get; set; }

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }
}