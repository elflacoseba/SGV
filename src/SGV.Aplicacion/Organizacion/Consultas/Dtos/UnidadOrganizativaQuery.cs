namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for unidad organizativa queries.
/// </summary>
public enum UnidadOrganizativaSegmentoListado
{
    /// <summary>Return only active (non-deleted) units. This is the default.</summary>
    Activas = 0,
    /// <summary>Return only soft-deleted units.</summary>
    Eliminadas = 1
}

/// <summary>
/// Query parameters for paginated, filtered listing of organizational units.
/// All filters are optional; omitting them returns all active units.
/// </summary>
public sealed record UnidadOrganizativaQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? TipoUnidadOrganizativaId = null,
    Guid? UnidadPadreId = null,
    DateOnly? VigenteEn = null,
    UnidadOrganizativaSegmentoListado Segmento = UnidadOrganizativaSegmentoListado.Activas);
