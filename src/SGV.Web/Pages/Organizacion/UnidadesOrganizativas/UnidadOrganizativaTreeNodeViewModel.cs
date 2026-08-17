using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// View model used to render a hierarchical organizational unit tree in Razor.
/// <see cref="Vigencia"/> combina texto + clase CSS opcional para colorear
/// el badge (issue #281).
/// <para>
/// <see cref="EsVigente"/> es la proyección de la ventana de vigencia
/// calculada en el shell para que el JavaScript pueda filtrar
/// visualmente las unidades no vigentes (issue #286). Un rango
/// <c>null</c>/<c>null</c> se considera vigente por convención del
/// dominio (<c>UnidadOrganizativa.EsVigente</c>).
/// </para>
/// </summary>
public sealed record UnidadOrganizativaTreeNodeViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    VigenciaViewModel Vigencia,
    bool EsVigente,
    IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> Children);