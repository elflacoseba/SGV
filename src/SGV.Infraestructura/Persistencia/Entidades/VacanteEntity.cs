namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Vacante.
/// </summary>
public sealed class VacanteEntity : AuditableEntityBase
{
    public Guid PuestoId { get; set; }

    public PuestoEntity Puesto { get; set; } = null!;

    public Guid EstadoVacanteId { get; set; }

    public EstadoVacanteEntity EstadoVacante { get; set; } = null!;

    public DateTime FechaApertura { get; set; }

    public DateTime? FechaCierre { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public List<HistorialEstadoVacanteEntity> HistorialEstados { get; set; } = [];

    public List<PostulacionEntity> Postulaciones { get; set; } = [];
}
