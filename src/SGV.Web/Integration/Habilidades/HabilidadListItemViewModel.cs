using System.Net;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// View model de grilla para el listado web de habilidades (catálogo maestro).
/// </summary>
public sealed record HabilidadListItemViewModel(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string? Categoria);

/// <summary>
/// Contrato de consulta para el listado web de habilidades.
/// <c>Status</c> se mapea al query string <c>status</c> de la API
/// (<c>activas</c> por defecto, <c>eliminadas</c> para vista de eliminados).
/// </summary>
public sealed record HabilidadListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    string? Status = null);

/// <summary>
/// Resultado de la baja lógica de una habilidad traducido desde la API.
/// </summary>
public sealed record HabilidadDeleteResult(
    bool Succeeded,
    HttpStatusCode? StatusCode,
    string? Code,
    string? Message);