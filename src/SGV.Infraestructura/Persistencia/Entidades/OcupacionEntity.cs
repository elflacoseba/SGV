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

    /// <summary>
    /// FK opcional a <see cref="VacanteEntity"/>. Se setea cuando la
    /// Ocupacion se crea a partir de Cubrir una Vacante (N2 del change
    /// <c>vacante-ocupacion-flow-alignment</c>). Nullable para soportar
    /// Ocupaciones históricas pre-N2 sin migración backfill.
    /// </summary>
    public Guid? VacanteId { get; set; }

    public VacanteEntity? Vacante { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public TipoAsignacion TipoAsignacion { get; set; }

    public string? Observaciones { get; set; }
}
