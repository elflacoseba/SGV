namespace SGV.Contracts.Vacantes.Consultas.Dtos;

/// <summary>
/// Wire-type consumer-safe de un estado de vacante del catálogo (solo lectura).
/// </summary>
public sealed record EstadoVacanteDto(
    Guid Id,
    string Codigo,
    string Nombre,
    int Orden,
    bool EsTerminal,
    bool EsCubierta);
