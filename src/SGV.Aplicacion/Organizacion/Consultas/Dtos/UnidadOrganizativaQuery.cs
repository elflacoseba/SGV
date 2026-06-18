namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

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
    DateOnly? VigenteEn = null);
