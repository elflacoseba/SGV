using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Aplicacion.Organizacion.Comandos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Maps a non-success <see cref="CargoCommandResult"/> into a
/// <see cref="ModelStateDictionary"/> so the Razor form can render the
/// errors next to the right fields. Behavior-preserving extraction of
/// the inline mapping that <c>Create.cshtml.cs</c> used to perform.
/// </summary>
public static class CargoPostResultMapper
{
    /// <summary>
    /// Applies the result's payload to <paramref name="modelState"/> and
    /// returns <c>true</c> when <see cref="CargoCommandResult.FieldErrors"/>
    /// was applied (per-field errors prefixed with
    /// <see cref="CargoFormKeys.InputPrefix"/>). Returns <c>false</c>
    /// otherwise, in which case the caller decides what to do next
    /// (e.g., set a general <c>ErrorMessage</c>).
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item>If <c>FieldErrors</c> has entries, each key+message is added
    /// under <c>Input.&lt;key&gt;</c> and the method returns <c>true</c>.</item>
    /// <item>Else, if <c>Error.Message</c> is set, a single model error is
    /// added under the empty key (so it renders in
    /// <c>asp-validation-summary="ModelOnly"</c>) and the method returns
    /// <c>false</c>.</item>
    /// <item>Else, no model error is added and the method returns
    /// <c>false</c> (null result, success result, or empty failure).</item>
    /// </list>
    /// </remarks>
    public static bool TryMap(CargoCommandResult? result, ModelStateDictionary modelState)
    {
        if (result?.FieldErrors is { Count: > 0 } fieldErrors)
        {
            CargoFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors);
            return true;
        }

        var errorMessage = result?.Error?.Message;
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            modelState.AddModelError(string.Empty, errorMessage);
        }

        return false;
    }
}
