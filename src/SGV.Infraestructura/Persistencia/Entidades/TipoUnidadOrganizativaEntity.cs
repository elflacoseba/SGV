namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia de TipoUnidadOrganizativa.
/// </summary>
public sealed class TipoUnidadOrganizativaEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
}
