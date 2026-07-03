using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Helpers para construir URLs internas del módulo web de Habilidades.
/// </summary>
internal static class HabilidadFormHelpers
{
    /// <summary>
    /// Construye la URL de retorno al listado preservando filtros.
    /// <paramref name="page"/> se normaliza a entero ≥ 1.
    /// </summary>
    public static string BuildReturnToListUrl(IUrlHelper url, int page, string? search, string? sort)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        return url.Page("/Organizacion/Habilidades/Index", new
        {
            p = normalizedPage,
            search = normalizedSearch,
            sort = normalizedSort
        }) ?? "/organizacion/habilidades";
    }
}