using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// ViewModel de fila para el listado paginado de Ocupaciones. Proyección
/// del <see cref="OcupacionDto"/> wire a la grilla del shell web con los
/// flags de UI (<see cref="EsVigente"/>, <see cref="EsEliminada"/>,
/// <see cref="EsFinalizada"/>) que la Razor Page ramifica para mostrar u
/// ocultar las acciones por fila (REQ-OCC-LST-006).
/// </summary>
/// <remarks>
/// Modelado espejado de <c>PuestoListItemViewModel</c>: identificadores
/// opacos para grilla (<see cref="PersonaId"/>, <see cref="PuestoId"/>),
/// nombres aplanados para mostrar (<see cref="PersonaNombre"/>,
/// <see cref="PuestoNombre"/>), fechas como <see cref="DateOnly"/>
/// (cultura-específicas en la Razor Page, no acá) y <see cref="Estado"/>
/// propagado para ramificar el render.
/// </remarks>
public sealed record OcupacionListItemViewModel(
    Guid Id,
    Guid PersonaId,
    string PersonaNombre,
    Guid PuestoId,
    string PuestoNombre,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones,
    OcupacionEstado Estado)
{
    /// <summary><c>true</c> cuando la fila representa una ocupación vigente (activa, no finalizada ni eliminada).</summary>
    public bool EsVigente => Estado == OcupacionEstado.Vigente;

    /// <summary><c>true</c> cuando la fila representa una ocupación finalizada (con FechaFin y sin baja lógica).</summary>
    public bool EsFinalizada => Estado == OcupacionEstado.Finalizada;

    /// <summary><c>true</c> cuando la fila representa una ocupación eliminada (baja lógica).</summary>
    public bool EsEliminada => Estado == OcupacionEstado.Eliminada;

    /// <summary>Mapea un <see cref="OcupacionDto"/> al viewmodel de grilla.</summary>
    public static OcupacionListItemViewModel FromDto(OcupacionDto dto)
        => new(
            dto.Id,
            dto.PersonaId,
            dto.PersonaNombre,
            dto.PuestoId,
            dto.PuestoNombre,
            dto.FechaInicio,
            dto.FechaFin,
            dto.TipoAsignacion,
            dto.Observaciones,
            dto.Estado);
}