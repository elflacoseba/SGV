using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// POST handler extraction for <see cref="EditModel"/>. Encapsulates the
/// pre-populate → validate → execute → PRG / error flow.
/// <para>
/// Pre-populates immutable fields (Codigo, UnidadOrganizativaId, CargoId)
/// from the API before ModelState validation because Edit's form does NOT
/// render these fields (decision locked — they are immutable on an existing
/// Puesto). Their <c>[Required]</c> attributes from <see cref="PuestoInputModel"/>
/// would cause <c>ModelState.IsValid == false</c> if not populated first.
/// ModelState entries for these keys are then removed to avoid false
/// validation errors.
/// </para>
/// </summary>
internal static class PuestoEditPostHandler
{
    internal static async Task<IActionResult> HandleAsync(
        EditModel page, Guid id, CancellationToken ct)
    {
        // Pre-populate immutable fields from DB before ModelState validation
        try
        {
            var current = await page.PuestosApiClient.GetByIdAsync(id, ct);
            if (current is null)
            {
                page.IsRecoverable = true;
                page.ErrorMessage = "El puesto solicitado no está disponible.";
                page.Logger.LogWarning("Puesto with Id {PuestoId} was not found during POST.", id);
                return page.Page();
            }

            page.Input.Codigo = current.Codigo;
            page.Input.UnidadOrganizativaId = current.UnidadOrganizativaId;
            page.Input.CargoId = current.CargoId;

            // Remove ModelState errors from [Required] on immutable fields
            // that are not rendered in the Edit form
            page.ModelState.Remove(PuestoFormKeys.CodigoKey);
            page.ModelState.Remove(PuestoFormKeys.UnidadOrganizativaIdKey);
            page.ModelState.Remove(PuestoFormKeys.CargoIdKey);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            page.Logger.LogError(ex, "Failed to load puesto {Id} during POST prepopulate.", id);
            page.ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            var preservedError = page.ErrorMessage;
            await page.LoadCatalogsAsync(ct);
            if (string.IsNullOrWhiteSpace(page.ErrorMessage))
                page.ErrorMessage = preservedError;
            return page.Page();
        }

        if (!page.ModelState.IsValid)
        {
            await page.LoadCatalogsAsync(ct);
            return page.Page();
        }

        var request = new ActualizarPuestoRequest(
            page.Input.Nombre,
            string.IsNullOrWhiteSpace(page.Input.Descripcion)
                ? null
                : page.Input.Descripcion.Trim(),
            page.Input.PuestoSuperiorId);

        PuestoCommandResult result;
        try
        {
            result = await page.PuestosApiClient.UpdateAsync(id, request, ct);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            page.Logger.LogError(ex, "Puesto update transport failure.");
            page.ErrorMessage = PageFeedback.TransportMessage;
            page.ModelState.AddModelError(string.Empty, page.ErrorMessage);
            await page.LoadCatalogsAsync(ct);
            return page.Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            page.TempData[nameof(EditModel.StatusMessage)] =
                $"El puesto \"{result.Value.Nombre}\" se actualizó correctamente.";
            page.TempData[nameof(EditModel.StatusKind)] = "success";

            var nav = ReturnNavigationContext.FromQuery(
                p: page.ReturnPage,
                search: page.ReturnSearch,
                sort: page.ReturnSort,
                returnStatus: page.ReturnStatus);
            return page.RedirectToPage("/Organizacion/Puestos/Details", nav.ToRouteValues(id));
        }

        if (result.Error is not null)
        {
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = page.AuthRedirector.TryRedirectToLogin(page.Request.Path);
                if (redirect is not null)
                    return redirect;

                page.ErrorMessage = PageFeedback.UnauthorizedMessage;
                page.ModelState.AddModelError(string.Empty, page.ErrorMessage);
                await page.LoadCatalogsAsync(ct);
                return page.Page();
            }

            if (!PuestoPostResultMapper.TryMap(result, page.ModelState))
            {
                page.ErrorMessage = ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: "El puesto solicitado no está disponible.",
                    conflictMessage: "Conflicto al persistir el puesto.");
                page.ModelState.AddModelError(string.Empty, page.ErrorMessage);
            }
        }

        await page.LoadCatalogsAsync(ct);
        return page.Page();
    }
}
