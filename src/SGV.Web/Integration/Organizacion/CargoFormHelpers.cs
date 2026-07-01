using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Helper methods for the create/edit form of Cargos.
/// </summary>
public static class CargoFormHelpers
{
    /// <summary>
    /// Construye la URL de retorno al listado de cargos preservando los
    /// filtros de la página anterior (p, search, sort).
    /// </summary>
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort)
    {
        var baseUrl = url.Page("/Organizacion/Cargos/Index") ?? "/organizacion/cargos";
        var query = new List<KeyValuePair<string, string?>>();

        if (!string.IsNullOrWhiteSpace(page))
        {
            query.Add(new KeyValuePair<string, string?>("p", page));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add(new KeyValuePair<string, string?>("search", search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            query.Add(new KeyValuePair<string, string?>("sort", sort));
        }

        return query.Count == 0
            ? baseUrl
            : $"{baseUrl}{QueryString.Create(query)}";
    }

    /// <summary>
    /// Maps a <see cref="ValidationProblemDetails"/> field-errors dictionary
    /// into ModelState entries prefixed with <c>Input.</c> so the
    /// <c>asp-validation-for</c> tag helpers los rescaten.
    /// </summary>
    public static void ApplyFieldErrorsToModelState(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        if (fieldErrors is null) return;

        foreach (var (key, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError($"Input.{key}", message);
            }
        }
    }
}
