namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de Habilidad.
/// </summary>
public sealed class HabilidadEntity : AuditableEntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string? Categoria { get; set; }

    public bool IsActive { get; set; }
}
