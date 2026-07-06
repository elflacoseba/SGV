using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Input model para el formulario create/edit de un Puesto. La
/// página Edit (PR 3B) sólo edita <see cref="Nombre"/>,
/// <see cref="Descripcion"/> y <see cref="PuestoSuperiorId"/> — los demás
/// campos son inmutables — pero el modelo los declara porque el partial
/// <c>_Form.cshtml</c> los usa vía <c>asp-for</c> cuando
/// <see cref="IPuestoForm.IsEdit"/> es <c>false</c>.
/// </summary>
public sealed class PuestoInputModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Debe escoger una unidad organizativa.")]
    public Guid? UnidadOrganizativaId { get; set; }

    [Required(ErrorMessage = "Debe escoger un cargo.")]
    public Guid? CargoId { get; set; }

    [Display(Name = "Puesto superior")]
    public Guid? PuestoSuperiorId { get; set; }
}
