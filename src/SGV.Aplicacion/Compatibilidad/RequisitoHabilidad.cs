namespace SGV.Aplicacion.Compatibilidad;

public sealed record RequisitoHabilidad(
    Guid HabilidadId,
    byte NivelRequerido,
    decimal Ponderacion,
    bool EsObligatoria);
