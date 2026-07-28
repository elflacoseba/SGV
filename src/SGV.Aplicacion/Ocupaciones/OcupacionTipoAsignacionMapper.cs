using SGV.Contracts.Ocupaciones.Enums;
using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones;

/// <summary>
/// Maps between the wire <see cref="OcupacionTipoAsignacion"/> contract enum
/// and the persisted <see cref="TipoAsignacion"/> domain enum.
/// </summary>
/// <remarks>
/// Explicit name-based mapping instead of ordinal casting. This guarantees a
/// compile-time error if a new value is added to one enum but not the other,
/// preventing silent data corruption when the two enums drift.
/// </remarks>
public static class OcupacionTipoAsignacionMapper
{
    public static TipoAsignacion ToDomain(OcupacionTipoAsignacion contract) => contract switch
    {
        OcupacionTipoAsignacion.Permanente => TipoAsignacion.Permanente,
        OcupacionTipoAsignacion.Interina => TipoAsignacion.Interina,
        OcupacionTipoAsignacion.Temporal => TipoAsignacion.Temporal,
        _ => throw new ArgumentOutOfRangeException(nameof(contract), contract,
            $"OcupacionTipoAsignacion value '{contract}' has no domain mapping.")
    };

    public static OcupacionTipoAsignacion ToContract(TipoAsignacion domain) => domain switch
    {
        TipoAsignacion.Permanente => OcupacionTipoAsignacion.Permanente,
        TipoAsignacion.Interina => OcupacionTipoAsignacion.Interina,
        TipoAsignacion.Temporal => OcupacionTipoAsignacion.Temporal,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain,
            $"TipoAsignacion value '{domain}' has no contract mapping.")
    };
}
