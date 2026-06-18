namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de NivelCargo. Catálogo inmutable — no tiene IsActive/IsDeleted.
/// </summary>
public sealed class NivelCargoEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public byte ValorNumerico { get; set; }

    public int Orden { get; set; }
}
