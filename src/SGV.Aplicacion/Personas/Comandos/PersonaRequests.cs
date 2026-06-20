namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Request to create a new Persona.
/// </summary>
public sealed record CrearPersonaRequest(
    string Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    string? TipoDocumento = null,
    string? NumeroDocumento = null,
    string? Telefono = null
);

/// <summary>
/// Request to update editable fields of an existing Persona.
/// </summary>
public sealed record ActualizarPersonaRequest(
    string Legajo,
    string Nombres,
    string Apellidos,
    string? Email = null,
    string? TipoDocumento = null,
    string? NumeroDocumento = null,
    string? Telefono = null
);
