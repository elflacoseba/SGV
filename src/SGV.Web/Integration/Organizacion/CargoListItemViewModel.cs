using System.Net;
using SGV.Contracts.Comun;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// View model de grilla para el listado web de cargos activos.
/// </summary>
public sealed record CargoListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Nivel);

/// <summary>
/// Contrato de consulta para el listado web de cargos.
/// <c>Status</c> se mapea al query string <c>status</c> de la API
/// (<c>activas</c> por defecto, <c>eliminadas</c> para vista de eliminados).
/// </summary>
public sealed record CargoListQuery(int Page, int PageSize, string? Search, string? Sort, string? Status = null);

/// <summary>
/// Resultado de la baja lógica de un cargo traducida desde la API.
/// A partir del change <c>2026-07-13-taxonomia-errores-commandresult</c>
/// (issue #125) el record expone además <see cref="Categoria"/> para
/// alinear la forma con los demás <c>*DeleteResult</c> y permitir que la
/// Razor Page ramifique por la nueva taxonomía común
/// <see cref="ErrorCategoria"/>.
/// </summary>
public sealed record CargoDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message,
    ErrorCategoria Categoria = ErrorCategoria.NotFound);