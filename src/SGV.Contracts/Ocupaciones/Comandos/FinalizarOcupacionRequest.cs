namespace SGV.Contracts.Ocupaciones.Comandos;

public sealed record FinalizarOcupacionRequest(
    DateOnly FechaFin,
    string? Observaciones = null);
