namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de HistorialEstadoVacante.
/// </summary>
public sealed class HistorialEstadoVacanteEntity : EntityBase
{
    public Guid VacanteId { get; set; }

    public VacanteEntity Vacante { get; set; } = null!;

    public Guid? EstadoAnteriorId { get; set; }

    public EstadoVacanteEntity? EstadoAnterior { get; set; }

    public Guid EstadoNuevoId { get; set; }

    public EstadoVacanteEntity EstadoNuevo { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public string? ChangedByUserId { get; set; }

    public string? Motivo { get; set; }
}
