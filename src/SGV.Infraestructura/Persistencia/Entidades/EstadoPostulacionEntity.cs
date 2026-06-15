namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de EstadoPostulacion.
/// </summary>
public sealed class EstadoPostulacionEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int Orden { get; set; }

    public bool EsTerminal { get; set; }

    public bool EsTerminalPositivo { get; set; }
}
