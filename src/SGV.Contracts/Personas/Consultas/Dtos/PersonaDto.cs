namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Persona. Excludes audit, navigation properties, and internal tracking fields.
/// Wire-type living in <c>SGV.Contracts</c> so the web shell can consume it without depending on
/// <c>SGV.Aplicacion.Personas</c>. JSON shape MUST stay identical to the historic
/// <c>SGV.Aplicacion.Personas.Consultas.Dtos.PersonaDto</c> contract.
/// </summary>
public sealed record PersonaDto(
    Guid Id,
    string? Legajo,
    string Nombres,
    string Apellidos,
    string? Email,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Telefono,
    bool IsActive);