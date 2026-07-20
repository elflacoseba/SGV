namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Persona. Excludes audit, navigation properties, and internal tracking fields.
/// Wire-type living in <c>SGV.Contracts</c> so the web shell can consume it without depending on
/// <c>SGV.Aplicacion.Personas</c>.
///
/// Issue #147: <c>TipoDocumento</c> (string) is replaced by the FK
/// <c>TipoDocumentoId</c> + denormalized <c>TipoDocumentoCodigo</c> /
/// <c>TipoDocumentoNombre</c> via the joined <c>TipoDocumento</c> catalog DTO.
/// </summary>
public sealed record PersonaDto(
    Guid Id,
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email,
    Guid? TipoDocumentoId,
    string? TipoDocumentoCodigo,
    string? TipoDocumentoNombre,
    string? NumeroDocumento,
    string? Telefono,
    bool IsActive);