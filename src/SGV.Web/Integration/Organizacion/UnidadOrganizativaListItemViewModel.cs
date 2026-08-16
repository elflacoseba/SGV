using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// View model for the unidades organizativas listing page.
/// <see cref="Vigencia"/> combina texto + clase CSS opcional para
/// colorear el badge (issue #281).
/// </summary>
public sealed record UnidadOrganizativaListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    string? Descripcion,
    Guid? UnidadPadreId,
    VigenciaViewModel Vigencia);

/// <summary>
/// Query contract for the unidades organizativas listing page.
/// <c>VigenteEn</c> se mapea al query string <c>vigenteEn</c> de la
/// API; la capa de presentación lo inyecta desde
/// <see cref="VigenciaViewModel.Desde"/> como fecha de referencia
/// (issue #281).
/// </summary>
public sealed record UnidadOrganizativaListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    string? Status = null,
    DateOnly? VigenteEn = null);

/// <summary>
/// Delete result contract for the unidades organizativas listing page.
/// A partir del change <c>2026-07-13-taxonomia-errores-commandresult</c>
/// (issue #125) el record expone además <see cref="Categoria"/> para
/// alinear la forma con los demás <c>*DeleteResult</c>.
/// </summary>
public sealed record UnidadOrganizativaDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message,
    ErrorCategoria Categoria = ErrorCategoria.NotFound);
