using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// PageModel de Details del módulo web de Ocupaciones (Slice 3a del change
/// <c>2026-07-28-web-ocupaciones-issue-208</c>). Carga la ocupación por id
/// y expone el render readonly con los datos del DTO. Las acciones de ciclo
/// de vida (Finalizar/Eliminar/Reactivar) están gateadas por Admin +
/// estado vigente/finalizada y se implementan como handlers POST
/// separados que respetan el patrón PRG.
/// </summary>
/// <remarks>
/// Cumple REQ-OCC-FORM-003 (acciones por estado), REQ-OCC-FORM-007
/// (FechaFin &gt;= FechaInicio, validación cliente+servidor) y
/// REQ-OCC-FORM-008 (reactivación con feedback de colisión 409).
/// <para>
/// Slice 3 del change <c>reusable-persona-card</c> (issue #219): inyecta
/// <see cref="IPersonaApiClient"/> para enriquecer la card readonly con el
/// <see cref="PersonaDto"/> resuelto desde el backend. Sobre 404 o
/// falla de transporte cae silenciosamente a <c>PersonaNombre</c> sin
/// marcar <see cref="IsNotFound"/> (PER-CARD-06).
/// </para>
/// </remarks>
[Authorize]
public sealed class DetailsModel(
    IOcupacionApiClient ocupacionApiClient,
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<DetailsModel> logger) : PageModel
{
    /// <summary>DTO wire + flags de la ocupación mostrada.</summary>
    public OcupacionDetailsViewModel? ViewModel { get; private set; }

    /// <summary>Bandera de estado no encontrado (404 o falla de carga).</summary>
    public bool IsNotFound { get; private set; }

    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler. Carga la ocupación vía
    /// <see cref="IOcupacionApiClient.ObtenerPorIdAsync"/> y popula
    /// <see cref="ViewModel"/>. Si el recurso no se encuentra o el
    /// endpoint falla, marca <see cref="IsNotFound"/> para que la vista
    /// muestre un estado recuperable sin acciones de mutación. Cuando la
    /// ocupación está disponible, intenta enriquecer la card de Persona
    /// vinculada vía <see cref="TryLoadPersonaVinculadaAsync"/>; sobre
    /// 404 o falla de transporte, la card cae al fallback
    /// <c>PersonaNombre</c> sin propagar error.
    /// </summary>
    public async Task OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await ocupacionApiClient.ObtenerPorIdAsync(id, cancellationToken);
            if (dto is null)
            {
                IsNotFound = true;
                logger.LogWarning("Ocupacion with Id {Id} was not found or is no longer available.", id);
                return;
            }

            ViewModel = OcupacionDetailsViewModel.FromDto(dto);
            ViewModel.EsAdministrador = EsAdministrador;
            await TryLoadPersonaVinculadaAsync(dto.PersonaId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            IsNotFound = true;
            logger.LogError(ex, "Failed to load ocupacion with Id {Id}.", id);
        }
    }

    /// <summary>
    /// Enriquece <see cref="OcupacionDetailsViewModel.Persona"/> vía
    /// <see cref="IPersonaApiClient.GetByIdAsync"/>. 404, fallo de
    /// transporte y <see cref="Guid.Empty"/> son no-bloqueantes: la vista
    /// cae al fallback <c>PersonaNombre</c> del DTO wire sin marcar
    /// <see cref="IsNotFound"/> (la ocupación sí existe; sólo se degrada
    /// la card). Espejo 1-a-1 de
    /// <c>Usuarios/DetailsModel.TryLoadPersonaVinculadaAsync</c>
    /// (PR #168 / Slice 2).
    /// </summary>
    private async Task TryLoadPersonaVinculadaAsync(
        Guid personaId,
        CancellationToken cancellationToken)
    {
        if (personaId == Guid.Empty)
        {
            return;
        }

        try
        {
            ViewModel!.Persona = await personaApiClient
                .GetByIdAsync(personaId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Failed to enrich linked persona {PersonaId} for ocupacion detail; falling back to PersonaNombre.",
                personaId);
            ViewModel!.Persona = null;
        }
    }

    /// <summary>
    /// POST handler para <c>handler=Finalizar</c>. Recibe
    /// <paramref name="fechaFin"/> y <paramref name="observaciones"/>,
    /// valida cliente <c>FechaFin &gt;= FechaInicio</c>, llama al API y
    /// redirige con feedback (REQ-OCC-FORM-007).
    /// </summary>
    public async Task<IActionResult> OnPostFinalizarAsync(
        Guid id,
        DateOnly fechaFin,
        string? observaciones,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        var current = await SafeLoadAsync(id, cancellationToken);
        if (current is null)
        {
            return RedirectToPage(new { id });
        }

        // REQ-OCC-FORM-007: bloqueo cliente de FechaFin < FechaInicio.
        if (fechaFin < current.FechaInicio)
        {
            PageFeedback.SetWarning(
                TempData,
                "La fecha de fin debe ser igual o posterior a la fecha de inicio.");
            return RedirectToPage(new { id });
        }

        var request = new FinalizarOcupacionRequest(
            fechaFin,
            string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim());

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.FinalizarAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion finalize transport failure for {Id}.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return RedirectToPage(new { id });
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(
                TempData,
                $"La ocupación de {result.Value!.PersonaNombre} se finalizó correctamente.");
            return RedirectToPage(new { id });
        }

        if (result.Error is not null)
        {
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }
            }

            PageFeedback.SetDanger(
                TempData,
                ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                    conflictMessage: result.Error.Message));
        }

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// POST handler para <c>handler=Eliminar</c>. Ejecuta baja lógica y
    /// redirige al Index con feedback (REQ-OCC-FORM-003).
    /// </summary>
    public async Task<IActionResult> OnPostEliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.EliminarAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion delete transport failure for {Id}.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return RedirectToPage("/Organizacion/Ocupaciones/Index");
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "La ocupación se eliminó correctamente.");
            return RedirectToPage("/Organizacion/Ocupaciones/Index");
        }

        if (result.Error is not null)
        {
            if (result.Error.Categoria == ErrorCategoria.NotFound)
            {
                PageFeedback.SetWarning(TempData, PageFeedback.NotFoundDeleteMessage);
            }
            else if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                PageFeedback.SetDanger(TempData, PageFeedback.UnauthorizedMessage);
            }
            else
            {
                PageFeedback.SetDanger(
                    TempData,
                    ErrorCategoryMapper.Map(
                        result.Error.Categoria,
                        notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                        conflictMessage: result.Error.Message));
            }
        }

        return RedirectToPage("/Organizacion/Ocupaciones/Index");
    }

    /// <summary>
    /// POST handler para <c>handler=Reactivar</c>. Mapea 409 por
    /// colisión (<c>PersonaYPuestoOcupados</c> / <c>PuestoOcupado</c> /
    /// <c>OcupacionYaActiva</c>) a feedback con código funcional visible
    /// (REQ-OCC-FORM-008).
    /// </summary>
    public async Task<IActionResult> OnPostReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.ReactivarAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion reactivate transport failure for {Id}.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return RedirectToPage(new { id });
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(
                TempData,
                $"La ocupación de {result.Value!.PersonaNombre} se reactivó correctamente.");
            return RedirectToPage(new { id });
        }

        if (result.Error is not null)
        {
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                PageFeedback.SetDanger(TempData, PageFeedback.UnauthorizedMessage);
            }
            else if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                var message = result.Error.Code switch
                {
                    OcupacionErrorCodigo.PersonaYPuestoOcupados or
                    OcupacionErrorCodigo.PuestoOcupado or
                    OcupacionErrorCodigo.OcupacionYaActiva => $"{result.Error.Code}: {result.Error.Message}",
                    _ => result.Error.Message
                };
                PageFeedback.SetDanger(TempData, message);
            }
            else
            {
                PageFeedback.SetDanger(
                    TempData,
                    ErrorCategoryMapper.Map(
                        result.Error.Categoria,
                        notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                        conflictMessage: result.Error.Message));
            }
        }

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Carga la ocupación vía API; devuelve <c>null</c> y setea
    /// <see cref="TempData"/> con feedback de error si el recurso no
    /// existe o si hay una falla de transporte. Usado por los handlers
    /// POST que necesitan el <c>FechaInicio</c> para validación local
    /// (REQ-OCC-FORM-007).
    /// </summary>
    private async Task<OcupacionDto?> SafeLoadAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await ocupacionApiClient.ObtenerPorIdAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load ocupacion {Id} during POST prepopulate.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return null;
        }
    }
}