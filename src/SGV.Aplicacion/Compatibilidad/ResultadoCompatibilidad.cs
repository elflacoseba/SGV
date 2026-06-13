namespace SGV.Aplicacion.Compatibilidad;

public sealed record ResultadoCompatibilidad(
    decimal Puntaje,
    string Categoria,
    bool CumplePerfilCompleto,
    IReadOnlyCollection<ResultadoCompatibilidadHabilidad> Detalles);
