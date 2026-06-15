namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Postulacion.
/// </summary>
public sealed class PostulacionEntity : AuditableEntityBase
{
    public Guid VacanteId { get; set; }

    public VacanteEntity Vacante { get; set; } = null!;

    public Guid PostulanteId { get; set; }

    public PostulanteEntity Postulante { get; set; } = null!;

    public Guid EstadoPostulacionId { get; set; }

    public EstadoPostulacionEntity EstadoPostulacion { get; set; } = null!;

    public DateTime FechaPostulacion { get; set; }

    public decimal? PuntajeCompatibilidad { get; set; }

    public string? NivelCompatibilidad { get; set; }

    public string? Observaciones { get; set; }

    public List<HistorialEstadoPostulacionEntity> HistorialEstados { get; set; } = [];

    public List<EvaluacionPostulacionEntity> Evaluaciones { get; set; } = [];
}
