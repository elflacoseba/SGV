namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Postulante.
/// </summary>
public sealed class PostulanteEntity : AuditableEntityBase
{
    public Guid? PersonaId { get; set; }

    public PersonaEntity? Persona { get; set; }

    public string? Nombres { get; set; }

    public string? Apellidos { get; set; }

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public string? Fuente { get; set; }

    public string? Observaciones { get; set; }

    public List<PostulacionEntity> Postulaciones { get; set; } = [];
}
