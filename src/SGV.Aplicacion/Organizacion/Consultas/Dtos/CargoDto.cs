namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Cargo. Excludes audit and internal tracking fields.
/// </summary>
public sealed record CargoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid NivelId,
    string? NivelNombre = null
);
