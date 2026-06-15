namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Cargo.
/// </summary>
public sealed class CargoEntity : AuditableEntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string? Nivel { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Habilidades requeridas por el cargo.
    /// </summary>
    public List<CargoHabilidadEntity> Habilidades { get; set; } = [];

    /// <summary>
    /// Puestos que corresponden a este cargo.
    /// </summary>
    public List<PuestoEntity> Puestos { get; set; } = [];
}
