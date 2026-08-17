namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// A node in the hierarchical tree of organizational units.
/// Each node carries its own data and a list of child nodes (Hijas).
/// </summary>
/// <remarks>
/// <see cref="VigenteDesde"/> / <see cref="VigenteHasta"/> were added in
/// issue #286 so the web shell can offer a visual filter for units whose
/// validity window has already closed. The fields are nullable to keep
/// backwards compatibility with cached trees that pre-date the change;
/// a null range renders as <c>EsVigente = true</c> in the web viewmodel.
/// </remarks>
public sealed record UnidadOrganizativaTreeNodeDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string TipoUnidadNombre,
    IReadOnlyList<UnidadOrganizativaTreeNodeDto> Hijas,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null);