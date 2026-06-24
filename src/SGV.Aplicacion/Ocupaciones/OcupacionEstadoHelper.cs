using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones;

/// <summary>
/// Shared helper for computing Ocupacion display state.
/// </summary>
public static class OcupacionEstadoHelper
{
    /// <summary>
    /// Computes the display state string from the domain entity.
    /// </summary>
    public static string CalcularEstado(Ocupacion ocupacion)
    {
        if (ocupacion.IsDeleted)
            return "Eliminado";
        if (ocupacion.FechaFin is not null)
            return "Finalizado";
        return "Activo";
    }
}