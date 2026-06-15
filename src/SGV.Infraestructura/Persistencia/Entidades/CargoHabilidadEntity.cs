namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de CargoHabilidad.
/// </summary>
public sealed class CargoHabilidadEntity : EntityBase
{
    public Guid CargoId { get; set; }

    public CargoEntity Cargo { get; set; } = null!;

    public Guid HabilidadId { get; set; }

    public HabilidadEntity Habilidad { get; set; } = null!;

    public Guid NivelRequeridoId { get; set; }

    public NivelHabilidadEntity NivelRequerido { get; set; } = null!;

    public decimal Ponderacion { get; set; }

    public bool EsObligatoria { get; set; }
}
