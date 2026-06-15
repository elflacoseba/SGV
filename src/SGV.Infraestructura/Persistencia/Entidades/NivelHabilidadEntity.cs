namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de NivelHabilidad.
/// </summary>
public sealed class NivelHabilidadEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public byte ValorNumerico { get; set; }

    public int Orden { get; set; }
}
