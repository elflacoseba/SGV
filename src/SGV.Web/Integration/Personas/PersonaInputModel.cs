using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Input model para los formularios <c>Create.cshtml</c> y
/// <c>Edit.cshtml</c> del módulo Personas. Refleja los campos del
/// <c>CrearPersonaRequest</c>/<c>ActualizarPersonaRequest</c> de
/// <c>SGV.Contracts</c>; las validaciones DataAnnotations vigentes
/// son las del backend, pero el cliente las repite aquí como defensa
/// de UX (cliente nunca enviaría 400 por campos vacíos obvios). El
/// backend sigue siendo la fuente de verdad.
/// </summary>
public sealed class PersonaInputModel
{
    [Required(ErrorMessage = "El legajo es obligatorio.")]
    [StringLength(20, ErrorMessage = "El legajo no puede superar los 20 caracteres.")]
    public string Legajo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los nombres no pueden superar los 100 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, ErrorMessage = "Los apellidos no pueden superar los 100 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [StringLength(200, ErrorMessage = "El email no puede superar los 200 caracteres.")]
    public string? Email { get; set; }

    /// <summary>
    /// Legacy free-form tipo de documento (deprecated by issue #147).
    /// Se mantiene en el input model para preservar el binding con el
    /// <c>&lt;select&gt;</c> existente; el backend ahora consume
    /// <see cref="TipoDocumentoId"/>. PR3 reemplazará el control con un
    /// <c>&lt;select name="TipoDocumentoId"&gt;</c> poblado desde
    /// <c>GetTiposDocumentoAsync</c>.
    /// </summary>
    [StringLength(20, ErrorMessage = "El tipo de documento no puede superar los 20 caracteres.")]
    public string? TipoDocumento { get; set; }

    /// <summary>
    /// FK hacia <c>TipoDocumento</c> (issue #147). Reemplaza
    /// <see cref="TipoDocumento"/> como la fuente de verdad wire; el campo
    /// string legacy se preserva por back-compat.
    /// </summary>
    public Guid? TipoDocumentoId { get; set; }

    [StringLength(30, ErrorMessage = "El número de documento no puede superar los 30 caracteres.")]
    public string? NumeroDocumento { get; set; }

    [StringLength(40, ErrorMessage = "El teléfono no puede superar los 40 caracteres.")]
    public string? Telefono { get; set; }
}
