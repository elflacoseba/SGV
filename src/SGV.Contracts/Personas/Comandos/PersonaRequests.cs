namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Request to create a new Persona (issue #147: TipoDocumentoId replaces the
/// legacy free-form <c>TipoDocumento</c> string). <c>Legajo</c> es opcional
/// (issue #202): el dominio y los validators ya admiten <c>null</c>; este
/// cambio alinea el wire-type para que el shell web pueda omitirlo o enviar
/// <c>legajo: null</c> sin workaround.
/// </summary>
public sealed record CrearPersonaRequest(
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    Guid? TipoDocumentoId = null,
    string? NumeroDocumento = null,
    string? Telefono = null);

/// <summary>
/// Request to update editable fields of an existing Persona (issue #147).
/// <c>Legajo</c> es opcional (issue #202): la transición no-nulo → null es
/// válida y la web debe poder limpiarla; el servicio de comandos emite una
/// fila de auditoría explícita cuando ocurre.
/// </summary>
public sealed record ActualizarPersonaRequest(
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    Guid? TipoDocumentoId = null,
    string? NumeroDocumento = null,
    string? Telefono = null);