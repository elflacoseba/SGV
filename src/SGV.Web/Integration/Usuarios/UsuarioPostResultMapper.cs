using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Maps a non-success <see cref="UsuarioCommandResult"/> into a
/// <see cref="ModelStateDictionary"/> so the Razor form can render the
/// errors next to the right fields. Espejo del
/// <see cref="Personas.PersonaPostResultMapper"/>: extrae la lógica de
/// mapping del CommandResult en un helper estático testeable y
/// desacoplable de la PageModel.
/// </summary>
/// <remarks>
/// <para>
/// PR2-HALL-1 (mini-PR correctivo): ahora que
/// <see cref="UsuarioCommandResult.FieldErrors"/> existe, este mapper
/// propaga los pares por-campo al ModelState prefijados con
/// <see cref="UsuarioFormKeys.InputPrefix"/> para que las tag helpers
/// <c>asp-validation-for</c> rendereen el mensaje junto al campo
/// correspondiente en la Razor Page de Create/Edit (PR 4).
/// </para>
/// <para>
/// Resolución:
/// <list type="number">
/// <item>Si <see cref="UsuarioCommandResult.FieldErrors"/> tiene
/// entradas, cada par clave→mensajes se agrega bajo
/// <c>Input.&lt;clave&gt;</c> y el método devuelve <c>true</c>.</item>
/// <item>Else, si <c>Error.Message</c> está poblado, un único error
/// de modelo se agrega bajo la clave vacía (para que se renderee en
/// <c>asp-validation-summary="ModelOnly"</c>) y el método devuelve
/// <c>false</c>.</item>
/// <item>Else, no se agrega ningún error de modelo y el método
/// devuelve <c>false</c> (result null, success, o fallo sin
/// detalle).</item>
/// </list>
/// </para>
/// </remarks>
public static class UsuarioPostResultMapper
{
    /// <summary>
    /// Aplica el payload del resultado a <paramref name="modelState"/> y
    /// devuelve <c>true</c> cuando el resultado contenía
    /// <see cref="UsuarioCommandResult.FieldErrors"/> con al menos una
    /// entrada (la PageModel puede mostrar feedback por-campo y omitir
    /// el banner de error general). Devuelve <c>false</c> cuando NO hay
    /// field errors (success, null, failure con sólo mensaje o vacío).
    /// </summary>
    public static bool TryMap(UsuarioCommandResult? result, ModelStateDictionary modelState)
    {
        if (result?.FieldErrors is { Count: > 0 } fieldErrors)
        {
            UsuarioFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors);
            return true;
        }

        var errorMessage = result?.Error?.Message;
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            modelState.AddModelError(string.Empty, errorMessage);
            return false;
        }

        return false;
    }
}
