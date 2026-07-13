using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Web.Integration.Common;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// POST handler extraction for <see cref="HabilidadesModel"/>. Each handler
/// encapsulates the check-admin → execute → transport-failure → success PRG
/// → error-handling flow. Called from the PageModel after admin check.
/// </summary>
internal static class CargoHabilidadesPostHandlers
{
    // ──────────────────────────────────────────────
    // POST Asignar
    // ──────────────────────────────────────────────

    internal static async Task<IActionResult> HandleAsignarAsync(
        HabilidadesModel page, Guid id, CancellationToken ct)
    {
        page.AsignarInput = CargoSkillFormHelpers.ReadAsignarInput(page.Request.Form, page.ModelState);

        if (!page.ModelState.IsValid)
        {
            await page.ReloadForFailureAsync(id, ct);
            return page.Page();
        }

        var request = new AsignarCargoSkillRequest(
            page.AsignarInput.NivelRequeridoId!.Value,
            page.AsignarInput.Ponderacion,
            page.AsignarInput.EsObligatoria);

        CargoSkillCommandResult result;
        try
        {
            result = await page.CargoApiClient.UpsertSkillAsync(
                id, page.AsignarInput.SkillId!.Value, request, ct);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            page.Logger.LogError(ex, "Cargo skill upsert transport failure.");
            page.ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            page.ModelState.AddModelError(string.Empty, page.ErrorMessage);
            await page.ReloadForFailureAsync(id, ct);
            return page.Page();
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(page.TempData,
                "La habilidad se asignó correctamente al cargo.");
            return page.RedirectToPage(new { id });
        }

        page.ErrorMessage = CargoSkillFormHelpers.ApplyAsignarFailureToModelState(result, page.ModelState);
        await page.ReloadForFailureAsync(id, ct);
        return page.Page();
    }

    // ──────────────────────────────────────────────
    // POST Actualizar
    // ──────────────────────────────────────────────

    internal static async Task<IActionResult> HandleActualizarAsync(
        HabilidadesModel page, Guid id, Guid skillId, CancellationToken ct)
    {
        if (!CargoSkillFormHelpers.TryReadActualizarRequest(
                skillId, page.Request.Form, page.ModelState, out var request))
        {
            await page.ReloadForFailureAsync(id, ct);
            return page.Page();
        }

        CargoSkillCommandResult result;
        try
        {
            result = await page.CargoApiClient.UpsertSkillAsync(
                id, skillId, request!, ct);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            page.Logger.LogError(ex, "Cargo skill update transport failure.");
            page.ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            page.ModelState.AddModelError(string.Empty, page.ErrorMessage);
            await page.ReloadForFailureAsync(id, ct);
            return page.Page();
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(page.TempData,
                "La habilidad del cargo se actualizó correctamente.");
            return page.RedirectToPage(new { id });
        }

        page.ErrorMessage = CargoSkillFormHelpers.ApplyActualizarFailureToModelState(
            skillId, result, page.ModelState);
        await page.ReloadForFailureAsync(id, ct);
        return page.Page();
    }

    // ──────────────────────────────────────────────
    // POST Quitar
    // ──────────────────────────────────────────────

    internal static async Task<IActionResult> HandleQuitarAsync(
        HabilidadesModel page, Guid id, Guid skillId, CancellationToken ct)
    {
        CargoSkillDeleteResult result;
        try
        {
            result = await page.CargoApiClient.DeleteSkillAsync(id, skillId, ct);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            page.Logger.LogError(ex, "Cargo skill delete transport failure.");
            PageFeedback.SetDanger(page.TempData,
                "No se pudo contactar al servicio de habilidades. Intentá nuevamente.");
            return page.RedirectToPage(new { id });
        }

        if (result.Succeeded)
        {
            PageFeedback.SetSuccess(page.TempData,
                "La habilidad se quitó del cargo correctamente.");
            return page.RedirectToPage(new { id });
        }

        // Unauthorized redirect via IAuthSessionRedirector
        if (result.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = page.AuthRedirector.TryRedirectToLogin(page.Request.Path);
            if (redirect is not null)
                return redirect;
        }

        // 404 = already gone
        if (result.Categoria == ErrorCategoria.NotFound)
        {
            PageFeedback.SetWarning(page.TempData,
                "La asociación ya no existe. La grilla fue actualizada.");
            return page.RedirectToPage(new { id });
        }

        var failureMessage = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : ErrorCategoryMapper.Map(result.Categoria);
        PageFeedback.SetDanger(page.TempData, failureMessage);
        return page.RedirectToPage(new { id });
    }
}
