namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de EvaluacionPostulacion.
/// </summary>
public sealed class EvaluacionPostulacionEntity : AuditableEntityBase
{
    public Guid PostulacionId { get; set; }

    public PostulacionEntity Postulacion { get; set; } = null!;

    public DateTime EvaluadoAt { get; set; }

    public string? EvaluadoByUserId { get; set; }

    public decimal? PuntajeTecnico { get; set; }

    public decimal? PuntajeEntrevista { get; set; }

    public decimal? PuntajeCompatibilidad { get; set; }

    public string? Recomendacion { get; set; }

    public string? Observaciones { get; set; }
}
