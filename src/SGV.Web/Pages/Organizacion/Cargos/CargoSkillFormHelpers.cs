using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// Form parsing and ModelState mapping helpers for the editable Cargo-Habilidad grid.
/// </summary>
public static class CargoSkillFormHelpers
{
    private static readonly HashSet<string> ActualizarFieldWhitelist =
        new(StringComparer.OrdinalIgnoreCase) { "NivelRequeridoId", "Ponderacion", "EsObligatoria" };

    public static CargoHabilidadAsignarInputModel ReadAsignarInput(IFormCollection form, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(modelState);

        var skillIdRaw = form["AsignarInput.SkillId"].ToString();
        var nivelRaw = form["AsignarInput.NivelRequeridoId"].ToString();
        var ponderacionRaw = form["AsignarInput.Ponderacion"].ToString();
        var esObligatoriaRaw = form["AsignarInput.EsObligatoria"].ToString();

        if (!Guid.TryParse(skillIdRaw, out var skillId) || skillId == Guid.Empty)
        {
            modelState.AddModelError("AsignarInput.SkillId", "Debe seleccionar una habilidad.");
        }

        if (!Guid.TryParse(nivelRaw, out var nivelId) || nivelId == Guid.Empty)
        {
            modelState.AddModelError("AsignarInput.NivelRequeridoId", "Debe seleccionar un nivel requerido.");
        }

        var (ponderacionValid, ponderacion) = CargoSkillPonderacionRule.TryParse(ponderacionRaw);
        if (!ponderacionValid)
        {
            modelState.AddModelError("AsignarInput.Ponderacion", CargoSkillPonderacionRule.ErrorMessage);
        }

        return new CargoHabilidadAsignarInputModel
        {
            SkillId = skillId == Guid.Empty ? null : skillId,
            NivelRequeridoId = nivelId == Guid.Empty ? null : nivelId,
            Ponderacion = ponderacion,
            EsObligatoria = string.Equals(esObligatoriaRaw, "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    public static bool TryReadActualizarRequest(
        Guid skillId,
        IFormCollection form,
        ModelStateDictionary modelState,
        out AsignarCargoSkillRequest? request)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(modelState);

        var nivelFormKey = $"Actualizar[{skillId}].NivelRequeridoId";
        var ponderacionFormKey = $"Actualizar[{skillId}].Ponderacion";
        var esObligatoriaFormKey = $"Actualizar[{skillId}].EsObligatoria";

        var nivelRaw = form[nivelFormKey].ToString();
        var ponderacionRaw = form[ponderacionFormKey].ToString();
        var esObligatoriaRaw = form[esObligatoriaFormKey].ToString();

        if (!Guid.TryParse(nivelRaw, out var nivelId) || nivelId == Guid.Empty)
        {
            modelState.AddModelError(nivelFormKey, "Debe seleccionar un nivel requerido.");
        }

        var (ponderacionValid, ponderacion) = CargoSkillPonderacionRule.TryParse(ponderacionRaw);
        if (!ponderacionValid)
        {
            modelState.AddModelError(ponderacionFormKey, CargoSkillPonderacionRule.ErrorMessage);
        }

        if (!modelState.IsValid)
        {
            request = null;
            return false;
        }

        request = new AsignarCargoSkillRequest(
            nivelId,
            ponderacion!.Value,
            string.Equals(esObligatoriaRaw, "true", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    public static string? ApplyAsignarFailureToModelState(CargoSkillCommandResult result, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        if (ApplyFieldErrors(result, modelState, key => key.StartsWith("AsignarInput.", StringComparison.OrdinalIgnoreCase) ? key : "AsignarInput." + key))
        {
            return null;
        }

        return ApplyErrorToModelState(result.Error, modelState);
    }

    public static string? ApplyActualizarFailureToModelState(Guid skillId, CargoSkillCommandResult result, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        // Anclaje por fila para los campos del whitelist {NivelRequeridoId,
        // Ponderacion, EsObligatoria}: el error se asocia a
        // `Actualizar[skillId].Campo` y lo renderiza el <div class="invalid-feedback">
        // de la fila correspondiente en Habilidades.cshtml. Para campos fuera del
        // whitelist (defensa contra drift), el error cae al `string.Empty` para que
        // el `<div asp-validation-summary="ModelOnly">` del formulario Asignar lo
        // muestre — sin anclarlo a ninguna fila incorrecta. Sin duplicación: cada
        // error va exactamente a un destino.
        if (ApplyFieldErrors(result, modelState, key =>
            ActualizarFieldWhitelist.Contains(key)
                ? $"Actualizar[{skillId}].{key}"
                : string.Empty))
        {
            return null;
        }

        return ApplyErrorToModelState(result.Error, modelState);
    }

    private static bool ApplyFieldErrors(
        CargoSkillCommandResult result,
        ModelStateDictionary modelState,
        Func<string, string> keySelector)
    {
        if (result.FieldErrors is not { Count: > 0 })
        {
            return false;
        }

        foreach (var kvp in result.FieldErrors)
        {
            var key = keySelector(kvp.Key);
            foreach (var fieldMessage in kvp.Value)
            {
                modelState.AddModelError(key, fieldMessage);
            }
        }

        return true;
    }

    private static string? ApplyErrorToModelState(CargoSkillError? error, ModelStateDictionary modelState)
    {
        if (error is null)
        {
            return null;
        }

        var message = error.Message;
        switch (error.Type)
        {
            case CargoSkillErrorType.NotFound:
                modelState.AddModelError(string.Empty, "El cargo o la habilidad solicitada no existe.");
                return null;
            case CargoSkillErrorType.Conflict:
                modelState.AddModelError(string.Empty, message);
                return null;
            case CargoSkillErrorType.Forbidden:
                modelState.AddModelError(string.Empty, "No tiene permisos para modificar las habilidades del cargo.");
                return "No tiene permisos para modificar las habilidades del cargo.";
            case CargoSkillErrorType.Unauthorized:
                modelState.AddModelError(string.Empty, "Su sesión expiró. Vuelva a iniciar sesión.");
                return "Su sesión expiró. Vuelva a iniciar sesión.";
            case CargoSkillErrorType.Transport:
                modelState.AddModelError(string.Empty, "El servicio no respondió correctamente. Intentá nuevamente.");
                return "El servicio no respondió correctamente. Intentá nuevamente.";
            default:
                modelState.AddModelError(string.Empty, message);
                return message;
        }
    }
}
