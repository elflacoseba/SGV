using SGV.Contracts.Ocupaciones.Enums;
using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones;

/// <summary>
/// Shared helper for computing Ocupacion display state.
/// </summary>
public static class OcupacionEstadoHelper
{
    /// <summary>
    /// Computes the wire state from the domain entity.
    /// </summary>
    public static OcupacionEstado CalcularEstado(Ocupacion ocupacion)
    {
        if (ocupacion.IsDeleted)
        {
            return OcupacionEstado.Eliminada;
        }

        return ocupacion.FechaFin is not null
            ? OcupacionEstado.Finalizada
            : OcupacionEstado.Vigente;
    }
}