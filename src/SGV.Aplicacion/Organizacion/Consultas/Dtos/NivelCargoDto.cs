namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for NivelCargo catalog.
/// </summary>
public sealed record NivelCargoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    byte ValorNumerico,
    int Orden
);
