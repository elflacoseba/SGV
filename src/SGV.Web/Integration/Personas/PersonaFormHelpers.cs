using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Stable binding keys used by the Persona Create/Edit form contract.
/// Centralized so the partial (<c>_Form.cshtml</c>), the page models
/// (<c>Create.cshtml.cs</c>, <c>Edit.cshtml.cs</c> en PR3) y tests
/// coinciden en los strings exactos que el model binder espera.
/// Issue #147 PR3: el legacy <c>TipoDocumentoKey</c> se elimina porque
/// el <c>&lt;input&gt;</c> string fue reemplazado por un
/// <c>&lt;select name="Input.TipoDocumentoId"&gt;</c> bindable directo.
/// </summary>
public static class PersonaFormKeys
{
    /// <summary>
    /// Common prefix used by Razor's <c>asp-for="Input.Xyz"</c> tag helpers
    /// and by <see cref="PersonaFormHelpers.ApplyFieldErrorsToModelState"/>.
    /// </summary>
    public const string InputPrefix = "Input.";

    /// <summary>Binding key for the <c>Legajo</c> field.</summary>
    public const string LegajoKey = InputPrefix + "Legajo";

    /// <summary>Binding key for the <c>Nombres</c> field.</summary>
    public const string NombresKey = InputPrefix + "Nombres";

    /// <summary>Binding key for the <c>Apellidos</c> field.</summary>
    public const string ApellidosKey = InputPrefix + "Apellidos";

    /// <summary>Binding key for the <c>Email</c> field.</summary>
    public const string EmailKey = InputPrefix + "Email";

    /// <summary>Binding key for the <c>TipoDocumentoId</c> field (issue #147).</summary>
    public const string TipoDocumentoIdKey = InputPrefix + "TipoDocumentoId";

    /// <summary>Binding key for the <c>NumeroDocumento</c> field.</summary>
    public const string NumeroDocumentoKey = InputPrefix + "NumeroDocumento";

    /// <summary>Binding key for the <c>Telefono</c> field.</summary>
    public const string TelefonoKey = InputPrefix + "Telefono";
}

/// <summary>
/// Helper methods for the create/edit form of Personas. Mirrors the
/// <c>CargoFormHelpers</c> shape so the partial de Create/Edit de
/// Personas (PR3) pueda escribir los errores de validación del backend
/// bajo los controles correctos usando las mismas constantes.
/// </summary>
public static class PersonaFormHelpers
{
    /// <summary>
    /// Maps a <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails"/>
    /// field-errors dictionary into ModelState entries prefixed with
    /// <see cref="PersonaFormKeys.InputPrefix"/> so the
    /// <c>asp-validation-for</c> tag helpers can render them next to
    /// the right field. El backend serializa las claves en
    /// <c>camelCase</c> (e.g. <c>legajo</c>, <c>numeroDocumento</c>);
    /// la composición <c>Input.&lt;clave&gt;</c> matchea por
    /// <c>OrdinalIgnoreCase</c> en Razor, así que la capitalización es
    /// cosmética: el helper deja la clave cruda.
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
                modelState.AddModelError(PersonaFormKeys.InputPrefix + key, message);
            }
        }
    }

    /// <summary>
    /// Construye la URL de retorno al listado de personas preservando los
    /// filtros de la página anterior (p, search, sort). Espejo del
    /// <c>CargoFormHelpers.BuildReturnToListUrl</c>.
    /// </summary>
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort)
    {
        var baseUrl = url.Page("/Personas/Index") ?? "/personas";
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
}
