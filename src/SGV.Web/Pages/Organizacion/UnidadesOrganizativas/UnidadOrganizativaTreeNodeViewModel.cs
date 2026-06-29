namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// View model used to render a hierarchical organizational unit tree in Razor.
/// </summary>
public sealed record UnidadOrganizativaTreeNodeViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> Children);
