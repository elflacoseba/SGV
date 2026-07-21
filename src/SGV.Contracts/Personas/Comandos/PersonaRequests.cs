namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Request to create a new Persona (issue #147: TipoDocumentoId replaces the
/// legacy free-form <c>TipoDocumento</c> string).
/// </summary>
public sealed record CrearPersonaRequest(
    string Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    Guid? TipoDocumentoId = null,
    string? NumeroDocumento = null,
    string? Telefono = null);

/// <summary>
/// Request to update editable fields of an existing Persona (issue #147).
/// </summary>
public sealed record ActualizarPersonaRequest(
    string Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    Guid? TipoDocumentoId = null,
    string? NumeroDocumento = null,
    string? Telefono = null);