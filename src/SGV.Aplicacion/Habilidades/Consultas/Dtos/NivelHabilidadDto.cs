namespace SGV.Aplicacion.Habilidades.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for NivelHabilidad (skill level).
/// Exposes the common catalog fields expected by consumers.
/// </summary>
public sealed record NivelHabilidadDto(
    Guid Id,
    string Codigo,
    string Nombre,
    byte ValorNumerico,
    int Orden
);
