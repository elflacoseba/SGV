using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// View model used to render a hierarchical organizational unit tree in Razor.
/// <see cref="Vigencia"/> brings a derived badge + text for the index page.
/// <para>
/// A partir de issue #286 (tercer feedback del operador) el filtro de
/// "unidades expiradas" se calcula ENTERAMENTE en el cliente usando las
/// fechas crudas <see cref="VigenteDesde"/> y <see cref="VigenteHasta"/>.
/// Anteriormente dependíamos de un <see cref="EsVigente"/> server-side,
/// pero daba resultados confusos para el operador cuando tenía unidades
/// sin <c>VigenteHasta</c> configurado. Exponer las fechas crudas le da
/// al JavaScript todo lo que necesita para recalcular sin ambigüedad.
/// </para>
/// </summary>
public sealed record UnidadOrganizativaTreeNodeViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    VigenciaViewModel Vigencia,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> Children);