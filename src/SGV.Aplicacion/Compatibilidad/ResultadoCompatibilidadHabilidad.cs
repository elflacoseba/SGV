namespace SGV.Aplicacion.Compatibilidad;

public sealed record ResultadoCompatibilidadHabilidad(
    Guid HabilidadId,
    byte NivelRequerido,
    byte? NivelPersona,
    decimal Ponderacion,
    decimal PuntajeNormalizado,
    bool EsObligatoria,
    bool CumpleNivelRequerido);
