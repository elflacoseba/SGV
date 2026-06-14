namespace SGV.Aplicacion.Habilidades.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Habilidad. Excludes audit and internal tracking fields.
/// </summary>
public sealed record HabilidadDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Categoria
);
