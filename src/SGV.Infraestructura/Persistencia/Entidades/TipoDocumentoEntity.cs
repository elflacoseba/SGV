namespace SGV.Infraestructura.Persistencia.Entidades;

/// <summary>
/// Persistencia del catálogo <c>TipoDocumento</c>. Catálogo inmutable — no tiene
/// <c>IsActive</c>/<c>IsDeleted</c> (ver REQ-TD-001 / REQ-SPA-EVOLUTION-001
/// condición #1).
/// </summary>
public sealed class TipoDocumentoEntity : EntityBase
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? PatronValidacion { get; set; }

    public int? LongitudMinima { get; set; }

    public int? LongitudMaxima { get; set; }
}
