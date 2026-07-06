using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Stable binding keys used by the Puesto Create/Edit form contract.
/// Centralized so the partial (<c>_Form.cshtml</c>), the page models
/// (<c>Create.cshtml.cs</c> in PR 3A, <c>Edit.cshtml.cs</c> in PR 3B) and
/// tests agree on the exact strings the model binder expects. Espejo de
/// <c>CargoFormKeys</c> extendido con los seis campos de Puesto.
/// </summary>
public static class PuestoFormKeys
{
    /// <summary>
    /// Common prefix used by Razor's <c>asp-for="Input.Xyz"</c> tag helpers
    /// and by <see cref="PuestoFormHelpers.ApplyFieldErrorsToModelState"/>.
    /// </summary>
    public const string InputPrefix = "Input.";

    /// <summary>Binding key for the <c>Codigo</c> field.</summary>
    public const string CodigoKey = InputPrefix + "Codigo";

    /// <summary>Binding key for the <c>Nombre</c> field.</summary>
    public const string NombreKey = InputPrefix + "Nombre";

    /// <summary>Binding key for the <c>Descripcion</c> field.</summary>
    public const string DescripcionKey = InputPrefix + "Descripcion";

    /// <summary>Binding key for the <c>UnidadOrganizativaId</c> field.</summary>
    public const string UnidadOrganizativaIdKey = InputPrefix + "UnidadOrganizativaId";

    /// <summary>Binding key for the <c>CargoId</c> field.</summary>
    public const string CargoIdKey = InputPrefix + "CargoId";

    /// <summary>Binding key for the <c>PuestoSuperiorId</c> field.</summary>
    public const string PuestoSuperiorIdKey = InputPrefix + "PuestoSuperiorId";
}

/// <summary>
/// Helper methods for the create/edit form of Puestos. Espejo de
/// <c>CargoFormHelpers</c> extendido con <see cref="BuildReturnToListUrl"/>
/// que acepta <paramref name="status"/> (forward-compat con el toggle
/// "Eliminadas" del listado).
/// </summary>
public static class PuestoFormHelpers
{
    /// <summary>
    /// Construye la URL de retorno al listado de puestos preservando los
    /// filtros de la página anterior (p, search, sort, status). Status se
    /// serializa sólo cuando es <c>"eliminadas"</c> (cualquier otro valor
    /// cae a <c>null</c> para no contaminar el query string cuando el
    /// segmento vigente es el default).
    /// </summary>
    public static string BuildReturnToListUrl(
        IUrlHelper url,
        string? page,
        string? search,
        string? sort,
        string? status)
    {
        var baseUrl = url.Page("/Organizacion/Puestos/Index") ?? "/organizacion/puestos";
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

        if (string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase))
        {
            query.Add(new KeyValuePair<string, string?>("status", "eliminadas"));
        }

        return query.Count == 0
            ? baseUrl
            : $"{baseUrl}{QueryString.Create(query)}";
    }

    /// <summary>
    /// Maps a backend <see cref="ValidationProblemDetails"/> field-errors
    /// dictionary (e.g., from a 400 with per-field errors) into ModelState
    /// entries prefixed with <see cref="PuestoFormKeys.InputPrefix"/> so the
    /// <c>asp-validation-for</c> tag helpers can render them next to the
    /// right field. El backend de Puestos emite las claves en camelCase
    /// (<c>codigo</c>, <c>nombre</c>, etc.); nosotros las prefijamos con
    /// <c>"Input."</c>.
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
                modelState.AddModelError(PuestoFormKeys.InputPrefix + key, message);
            }
        }
    }
}
