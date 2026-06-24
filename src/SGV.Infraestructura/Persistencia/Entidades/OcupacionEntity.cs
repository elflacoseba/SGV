using SGV.Dominio.Ocupaciones;

namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Ocupacion.
/// </summary>
public sealed class OcupacionEntity : AuditableEntityBase
{
    public Guid PersonaId { get; set; }

    public PersonaEntity Persona { get; set; } = null!;

    public Guid PuestoId { get; set; }

    public PuestoEntity Puesto { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public TipoAsignacion TipoAsignacion { get; set; }

    public string? Observaciones { get; set; }
}
