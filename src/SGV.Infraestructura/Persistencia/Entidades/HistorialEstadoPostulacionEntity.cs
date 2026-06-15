namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de HistorialEstadoPostulacion.
/// </summary>
public sealed class HistorialEstadoPostulacionEntity : EntityBase
{
    public Guid PostulacionId { get; set; }

    public PostulacionEntity Postulacion { get; set; } = null!;

    public Guid? EstadoAnteriorId { get; set; }

    public EstadoPostulacionEntity? EstadoAnterior { get; set; }

    public Guid EstadoNuevoId { get; set; }

    public EstadoPostulacionEntity EstadoNuevo { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public string? ChangedByUserId { get; set; }

    public string? Observaciones { get; set; }
}
