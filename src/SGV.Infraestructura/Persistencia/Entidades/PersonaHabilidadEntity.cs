namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de PersonaHabilidad.
/// </summary>
public sealed class PersonaHabilidadEntity : EntityBase
{
    public Guid PersonaId { get; set; }

    public PersonaEntity Persona { get; set; } = null!;

    public Guid HabilidadId { get; set; }

    public HabilidadEntity Habilidad { get; set; } = null!;

    public Guid NivelHabilidadId { get; set; }

    public NivelHabilidadEntity NivelHabilidad { get; set; } = null!;

    public DateTime? VerificadoAt { get; set; }

    public string? Fuente { get; set; }
}
