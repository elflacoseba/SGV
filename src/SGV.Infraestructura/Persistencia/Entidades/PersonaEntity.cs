namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Persona.
/// </summary>
public sealed class PersonaEntity : AuditableEntityBase
{
    public string? Legajo { get; set; }

    public string Nombres { get; set; } = string.Empty;

    public string Apellidos { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? TipoDocumento { get; set; }

    public string? NumeroDocumento { get; set; }

    public string? Telefono { get; set; }

    public bool IsActive { get; set; }

    public List<PersonaHabilidadEntity> Habilidades { get; set; } = [];

    public List<OcupacionEntity> Ocupaciones { get; set; } = [];
}
