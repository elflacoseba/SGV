namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Puesto.
/// </summary>
public sealed class PuestoEntity : AuditableEntityBase
{
    public Guid UnidadOrganizativaId { get; set; }

    public UnidadOrganizativaEntity UnidadOrganizativa { get; set; } = null!;

    public Guid CargoId { get; set; }

    public CargoEntity Cargo { get; set; } = null!;

    public Guid? PuestoSuperiorId { get; set; }

    public PuestoEntity? PuestoSuperior { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public bool IsActive { get; set; }

    public List<PuestoEntity> PuestosSubordinados { get; set; } = [];

    public List<OcupacionEntity> Ocupaciones { get; set; } = [];

    public List<VacanteEntity> Vacantes { get; set; } = [];
}
