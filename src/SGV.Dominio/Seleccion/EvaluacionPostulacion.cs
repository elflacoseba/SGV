using SGV.Dominio.Comun;

namespace SGV.Dominio.Seleccion;

public sealed class EvaluacionPostulacion : EntidadAuditable
{
    private EvaluacionPostulacion()
    {
    }

    public EvaluacionPostulacion(Guid postulacionId, DateTime evaluadoAt, string? evaluadoByUserId, string? observaciones)
    {
        PostulacionId = postulacionId;
        EvaluadoAt = evaluadoAt;
        EvaluadoByUserId = ValidacionesDominio.Opcional(evaluadoByUserId, nameof(EvaluadoByUserId), 450);
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 2000);
    }

    public Guid PostulacionId { get; private set; }

    public Postulacion Postulacion { get; private set; } = null!;

    public DateTime EvaluadoAt { get; private set; }

    public string? EvaluadoByUserId { get; private set; }

    public decimal? PuntajeTecnico { get; private set; }

    public decimal? PuntajeEntrevista { get; private set; }

    public decimal? PuntajeCompatibilidad { get; private set; }

    public string? Recomendacion { get; private set; }

    public string? Observaciones { get; private set; }

    public void RegistrarPuntajes(decimal? puntajeTecnico, decimal? puntajeEntrevista, decimal? puntajeCompatibilidad)
    {
        PuntajeTecnico = puntajeTecnico.HasValue ? ValidacionesDominio.Porcentaje(puntajeTecnico.Value, nameof(PuntajeTecnico)) : null;
        PuntajeEntrevista = puntajeEntrevista.HasValue ? ValidacionesDominio.Porcentaje(puntajeEntrevista.Value, nameof(PuntajeEntrevista)) : null;
        PuntajeCompatibilidad = puntajeCompatibilidad.HasValue ? ValidacionesDominio.Porcentaje(puntajeCompatibilidad.Value, nameof(PuntajeCompatibilidad)) : null;
    }

    public void Recomendar(string? recomendacion)
    {
        Recomendacion = ValidacionesDominio.Opcional(recomendacion, nameof(Recomendacion), 50);
    }
}
