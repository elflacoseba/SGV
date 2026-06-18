namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// A node in the hierarchical tree of organizational units.
/// Each node carries its own data and a list of child nodes (Hijas).
/// </summary>
public sealed record UnidadOrganizativaTreeNodeDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid TipoUnidadOrganizativaId,
    string TipoUnidadNombre,
    IReadOnlyList<UnidadOrganizativaTreeNodeDto> Hijas);
