using System.Net;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// View model for the unidades organizativas listing page.
/// </summary>
public sealed record UnidadOrganizativaListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    string? Descripcion,
    Guid? UnidadPadreId,
    string Vigencia);

/// <summary>
/// Query contract for the unidades organizativas listing page.
/// </summary>
public sealed record UnidadOrganizativaListQuery(int Page, int PageSize, string? Search, string? Sort);

/// <summary>
/// Delete result contract for the unidades organizativas listing page.
/// </summary>
public sealed record UnidadOrganizativaDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);
