namespace SGV.Aplicacion.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Persona. Excludes audit, navigation properties, and internal tracking fields.
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
    bool IsActive
);
