using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Stable binding keys used by the Cargo Create/Edit form contract.
/// Centralized so the partial (<c>_Form.cshtml</c>), the page models
/// (<c>Create.cshtml.cs</c>, <c>Edit.cshtml.cs</c> in PR2B) and tests
/// agree on the exact strings the model binder expects.
/// </summary>
public static class CargoFormKeys
{
    /// <summary>
    /// Common prefix used by Razor's <c>asp-for="Input.Xyz"</c> tag helpers
    /// and by <see cref="CargoFormHelpers.ApplyFieldErrorsToModelState"/>.
    /// </summary>
    public const string InputPrefix = "Input.";

    /// <summary>Binding key for the <c>Codigo</c> field.</summary>
    public const string CodigoKey = InputPrefix + "Codigo";

    /// <summary>Binding key for the <c>Nombre</c> field.</summary>
    public const string NombreKey = InputPrefix + "Nombre";

    /// <summary>Binding key for the <c>NivelId</c> field.</summary>
    public const string NivelIdKey = InputPrefix + "NivelId";

    /// <summary>Binding key for the <c>Descripcion</c> field.</summary>
    public const string DescripcionKey = InputPrefix + "Descripcion";
}

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
    /// into ModelState entries prefixed with <see cref="CargoFormKeys.InputPrefix"/>
    /// so the <c>asp-validation-for</c> tag helpers can render them next to
    /// the right field.
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
                modelState.AddModelError(CargoFormKeys.InputPrefix + key, message);
            }
        }
    }
}
