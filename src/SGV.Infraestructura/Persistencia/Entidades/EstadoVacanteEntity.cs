namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de EstadoVacante.
/// </summary>
public sealed class EstadoVacanteEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int Orden { get; set; }

    public bool EsTerminal { get; set; }
}
