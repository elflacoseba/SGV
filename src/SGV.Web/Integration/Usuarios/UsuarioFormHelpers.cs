using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Stable binding keys used by the Usuario Create/Edit form contract.
/// Centralized so the partial (<c>_Form.cshtml</c> en PR 4), los page
/// models (<c>Create.cshtml.cs</c>, <c>Edit.cshtml.cs</c> en PR 4) y los
/// tests acuerden en el string exacto que espera el model binder.
/// </summary>
public static class UsuarioFormKeys
{
    /// <summary>
    /// Prefix común usado por las tag helpers Razor <c>asp-for="Input.Xyz"</c>
    /// y por
    /// <see cref="UsuarioFormHelpers.ApplyFieldErrorsToModelState"/>.
    /// </summary>
    public const string InputPrefix = "Input.";

    /// <summary>Binding key para el campo <c>PersonaId</c>.</summary>
    public const string PersonaIdKey = InputPrefix + "PersonaId";

    /// <summary>Binding key para el campo <c>UserName</c>.</summary>
    public const string UserNameKey = InputPrefix + "UserName";

    /// <summary>Binding key para el campo <c>Email</c>.</summary>
    public const string EmailKey = InputPrefix + "Email";

    /// <summary>Binding key para el campo <c>Password</c>.</summary>
    public const string PasswordKey = InputPrefix + "Password";

    /// <summary>Binding key para el campo <c>Roles</c>.</summary>
    public const string RolesKey = InputPrefix + "Roles";
}

/// <summary>
/// Helpers para el formulario Create/Edit de Usuarios. Espejo del
/// <see cref="Personas.PersonaFormHelpers"/>: la partial de
/// Create/Edit de Usuarios (PR 4) escribirá los errores de validación
/// del backend bajo los controles correctos usando las constantes de
/// <see cref="UsuarioFormKeys"/>.
/// </summary>
public static class UsuarioFormHelpers
{
    /// <summary>
    /// Mapea el diccionario <c>field-errors</c> de un
    /// <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails"/> a
    /// entradas de <see cref="ModelStateDictionary"/> prefijadas con
    /// <see cref="UsuarioFormKeys.InputPrefix"/> para que las tag helpers
    /// <c>asp-validation-for</c> rendereen el error junto al campo
    /// correspondiente. El backend serializa las claves en
    /// <c>camelCase</c> (e.g. <c>userName</c>, <c>personaId</c>); la
    /// composición <c>Input.&lt;clave&gt;</c> matchea por
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
                modelState.AddModelError(UsuarioFormKeys.InputPrefix + key, message);
            }
        }
    }

    /// <summary>
    /// Construye la URL de retorno al listado de usuarios preservando
    /// los filtros de la página anterior (p, search, sort, status).
    /// Espejo del <c>PersonaFormHelpers.BuildReturnToListUrl</c>.
    /// </summary>
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort, string? status)
    {
        var baseUrl = url.Page("/Seguridad/Usuarios/Index") ?? "/seguridad/usuarios";
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

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add(new KeyValuePair<string, string?>("status", status));
        }

        return query.Count == 0
            ? baseUrl
            : $"{baseUrl}{QueryString.Create(query)}";
    }
}
