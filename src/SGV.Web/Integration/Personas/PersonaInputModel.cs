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
    /// FK hacia <c>TipoDocumento</c> (issue #147). El backend consume
    /// este Guid como la fuente de verdad wire; los validators de
    /// aplicación validan que el Id exista en el catálogo seed y que
    /// el <c>NumeroDocumento</c> matchee el patrón y rango del tipo.
    /// El page model hace binding desde el <c>&lt;select&gt;</c> poblado
    /// vía <see cref="SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto"/>
    /// cargado con <c>GetTiposDocumentoAsync</c> en OnGet.
    /// </summary>
    public Guid? TipoDocumentoId { get; set; }

    [StringLength(30, ErrorMessage = "El número de documento no puede superar los 30 caracteres.")]
    public string? NumeroDocumento { get; set; }

    [StringLength(40, ErrorMessage = "El teléfono no puede superar los 40 caracteres.")]
    public string? Telefono { get; set; }
}
