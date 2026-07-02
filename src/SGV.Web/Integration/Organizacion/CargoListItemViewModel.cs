using System.Net;

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
/// </summary>
public sealed record CargoDeleteResult(bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);