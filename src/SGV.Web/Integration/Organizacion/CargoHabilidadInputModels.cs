using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Input model bound to the "Asignar nueva habilidad" form on
/// <c>Pages/Organizacion/Cargos/Habilidades.cshtml</c>. Carga los campos
/// editables del vínculo <c>CargoHabilidad</c>: <c>SkillId</c>,
/// <c>NivelRequeridoId</c>, <c>Ponderacion</c> y <c>EsObligatoria</c>.
/// La validación <c>[Required]</c> corta antes de invocar al cliente API
/// y mantiene la página dentro de los mismos modelos de feedback que
/// <c>Create.cshtml.cs</c> / <c>Edit.cshtml.cs</c>.
/// </summary>
public sealed class CargoHabilidadAsignarInputModel
{
    [Required(ErrorMessage = "Debe seleccionar una habilidad.")]
    public Guid? SkillId { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un nivel requerido.")]
    public Guid? NivelRequeridoId { get; set; }

    [Range(0.01, 100.00, ErrorMessage = "La ponderación debe estar entre 0,01 y 100,00.")]
    public decimal? Ponderacion { get; set; }

    /// <summary>
    /// Default <c>false</c>; el modelo binder lo recibe como <c>true</c>
    /// cuando el checkbox está tildado y como <c>false</c> cuando no
    /// llega al form (default del bool). Mantenemos <c>bool</c> (no
    /// <c>bool?</c>) porque el estado "no enviado" se interpreta
    /// canónicamente como "no obligatoria".
    /// </summary>
    public bool EsObligatoria { get; set; }
}

/// <summary>
/// Input model bound to cada fila de la grilla editable de
/// <c>Habilidades.cshtml</c>. La ruta provee <c>cargoId</c> y
/// <c>skillId</c>; este input model carga los campos editables que
/// <see cref="ICargoApiClient.UpsertSkillAsync"/> recibe en el body.
/// NO incluye <c>SkillId</c> porque la skill del vínculo viaja en la
/// ruta, no en el body (alineado con el contrato del controller en PR2).
/// </summary>
public sealed class CargoHabilidadActualizarInputModel
{
    [Required(ErrorMessage = "Debe seleccionar un nivel requerido.")]
    public Guid? NivelRequeridoId { get; set; }

    [Range(0.01, 100.00, ErrorMessage = "La ponderación debe estar entre 0,01 y 100,00.")]
    public decimal? Ponderacion { get; set; }

    public bool EsObligatoria { get; set; }
}