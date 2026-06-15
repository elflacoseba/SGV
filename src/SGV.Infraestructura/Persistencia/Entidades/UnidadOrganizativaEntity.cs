namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de UnidadOrganizativa.
/// </summary>
public sealed class UnidadOrganizativaEntity : AuditableEntityBase
{
    public Guid? UnidadPadreId { get; set; }

    public UnidadOrganizativaEntity? UnidadPadre { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string TipoUnidad { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool IsActive { get; set; }

    public List<UnidadOrganizativaEntity> UnidadesHijas { get; set; } = [];

    public List<PuestoEntity> Puestos { get; set; } = [];
}
