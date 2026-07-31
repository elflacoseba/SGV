namespace SGV.Contracts.Vacantes.Comandos;

/// <summary>
/// Request para abrir una vacante.
/// </summary>
/// <remarks>
/// <paramref name="Motivo"/> es obligatorio al crear (lo exige el dominio).
/// <paramref name="Observaciones"/> es opcional y se normaliza a <see langword="null"/>
/// si llega vacío/whitespace (longitud máxima 500 caracteres).
/// </remarks>
public sealed record CrearVacanteRequest(
    Guid PuestoId,
    Guid EstadoVacanteId,
    DateTime FechaApertura,
    string Motivo,
    string? Observaciones = null);
