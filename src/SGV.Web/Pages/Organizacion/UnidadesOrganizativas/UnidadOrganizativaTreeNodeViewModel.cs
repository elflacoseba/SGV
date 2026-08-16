using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// View model used to render a hierarchical organizational unit tree in Razor.
/// <see cref="Vigencia"/> combina texto + clase CSS opcional para colorear
/// el badge (issue #281).
/// </summary>
public sealed record UnidadOrganizativaTreeNodeViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    VigenciaViewModel Vigencia,
    IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> Children);
