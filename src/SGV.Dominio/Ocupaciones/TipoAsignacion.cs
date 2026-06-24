namespace SGV.Dominio.Ocupaciones;

/// <summary>
/// Tipo de asignación de una ocupación. Los valores numéricos son contrato
/// persistido y no deben reordenarse ni renumerarse sin migración explícita.
/// </summary>
public enum TipoAsignacion
{
    /// <summary>Asignación permanente sin fecha de finalización prevista.</summary>
    Permanente = 0,

    /// <summary>Asignación interina con carácter suplente o provisional.</summary>
    Interina = 1,

    /// <summary>Asignación temporal por plazo definido.</summary>
    Temporal = 2,
}
