namespace SGV.Contracts.Vacantes.Comandos;

/// <summary>
/// Request para abrir una vacante.
/// </summary>
/// <remarks>
/// <paramref name="Motivo"/> es obligatorio al crear (lo exige el dominio).
/// <paramref name="Observaciones"/> es opcional y se normaliza a <see langword="null"/>
/// si llega vacío/whitespace (longitud máxima 500 caracteres).
/// <para>
/// <b>Issue #273 (Slice A):</b> <paramref name="EstadoVacanteId"/> es
/// <see cref="Nullable{T}"/> porque toda vacante nueva debe crearse en
/// estado "Abierta". Si el campo llega <see langword="null"/> o
/// <see cref="Guid.Empty"/>, la capa de Aplicación resuelve "Abierta"
/// desde el catálogo de <c>EstadosVacante</c>. Los consumidores que ya
/// envían un estado explícito siguen funcionando: el comando respeta el
/// ID provisto cuando es válido.
/// </para>
/// </remarks>
public sealed record CrearVacanteRequest(
    Guid PuestoId,
    Guid? EstadoVacanteId,
    DateTime FechaApertura,
    string Motivo,
    string? Observaciones = null);
