using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Habilidades;

/// <summary>
/// Helpers para construir URLs internas del módulo web de Habilidades.
/// </summary>
internal static class HabilidadFormHelpers
{
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort)
    {
        page ??= "1";
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        return url.Page("/Organizacion/Habilidades/Index", new
        {
            p = int.TryParse(page, out var p) && p > 0 ? p : 1,
            search = normalizedSearch,
            sort = normalizedSort
        }) ?? "/organizacion/habilidades";
    }
}