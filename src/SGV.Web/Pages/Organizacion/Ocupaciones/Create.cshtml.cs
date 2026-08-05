using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// PageModel de Create del módulo web de Ocupaciones (Slice 3a del
/// change <c>2026-07-28-web-ocupaciones-issue-208</c>). Carga los
/// catálogos Persona y Puesto vía <see cref="IPersonaApiClient.GetAllAsync"/>
/// y <see cref="IPuestosApiClient.GetAllAsync"/>, valida el formulario
/// y publica vía <see cref="IOcupacionApiClient.CrearAsync"/>. Sobre éxito
/// redirige al listado (PRG) preservando contexto. Sobre 409 mapea
/// <c>PersonaYPuestoOcupados</c>/<c>PuestoOcupado</c> al campo
/// correspondiente del <see cref="Microsoft.AspNetCore.Mvc.ModelStateDictionary"/>;
/// sobre <c>400</c> con <c>FieldErrors</c> aplica cada error a su campo.
/// Fallos de transporte se traducen a un error general recuperable y
/// conservan la entrada del usuario.
/// </summary>
/// <remarks>
/// Switch exhaustivo sobre <see cref="ErrorCategoria"/>.
/// <c>Unauthorized</c> redirige vía <see cref="IAuthSessionRedirector"/>;
/// <c>Forbidden</c> retorna <see cref="Forbid"/>.
/// Los códigos funcionales <c>PersonaYPuestoOcupados</c> /
/// <c>PuestoOcupado</c> del backend se preservan en
/// <see cref="OcupacionError.Code"/> y se mapean al campo correcto según
/// REQ-OCC-FORM-005 vía <see cref="OcupacionFormPageModel.MapConflictToModelState"/>.
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class CreateModel(
    IOcupacionApiClient ocupacionApiClient,
    IPersonaApiClient personaApiClient,
    IPuestosApiClient puestosApiClient,
    IVacanteApiClient vacanteApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : OcupacionFormPageModel
{
    /// <summary>Bandera estática para que la vista no muestre acciones de Edit.</summary>
    public override bool IsEdit => false;

    /// <summary>Mensaje de feedback (success/warning/danger) entregado vía TempData tras PRG.</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Tipo de feedback (success/warning/danger). Por defecto <c>success</c>.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// T-7.1 (change <c>vacante-ocupacion-flow-alignment</c>): true cuando
    /// el Puesto precargado (vía <c>?puestoId=</c>) NO tiene Vacante
    /// abierta. La vista usa este flag para mostrar el FORM-009 hint en
    /// variante <c>alert-warning</c>.
    /// </summary>
    public override bool PuestoSinVacanteAbierta
    {
        get => base.PuestoSinVacanteAbierta;
        protected set => base.PuestoSinVacanteAbierta = value;
    }

    /// <summary>
    /// GET handler. Pre-carga <see cref="OcupacionInputModel.PersonaId"/>
    /// y <see cref="OcupacionInputModel.PuestoId"/> desde el query string
    /// (paridad con la página cruzada <c>PersonaOcupaciones</c> de Slice 3b)
    /// y carga los catálogos Persona/Puesto en paralelo.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "personaId")] Guid? personaId = null,
        [FromQuery(Name = "puestoId")] Guid? puestoId = null,
        CancellationToken cancellationToken = default)
    {
        Input.PersonaId ??= personaId;
        Input.PuestoId ??= puestoId;

        await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);

        // T-7.1: si el Puesto ya viene precargado, consultamos si tiene
        // Vacante abierta para mostrar el hint correcto. Default false
        // (trata la falta de información como "sin vacante" para ser
        // conservative con la UI).
        if (Input.PuestoId.HasValue && Input.PuestoId.Value != Guid.Empty)
        {
            PuestoSinVacanteAbierta = !await vacanteApiClient
                .ExisteVacanteAbiertaParaPuestoAsync(Input.PuestoId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return Page();
    }

    /// <summary>
    /// POST handler. Valida <c>ModelState</c>; si pasa, llama
    /// <c>POST /api/v1/ocupaciones</c> y mapea el resultado. Sobre éxito
    /// redirige al listado (PRG) preservando filtros; sobre 409 mapea
    /// <c>PersonaYPuestoOcupados</c>/<c>PuestoOcupado</c> al campo
    /// correspondiente; sobre 400 con <c>FieldErrors</c> los aplica al
    /// <c>ModelState</c>; cualquier fallo recuperable (transporte,
    /// serialización) muestra error general y conserva input + catálogos.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await ReloadFormStateAsync(cancellationToken);
            return Page();
        }

        var request = new CrearOcupacionRequest(
            Input.PersonaId!.Value,
            Input.PuestoId!.Value,
            Input.FechaInicio!.Value,
            Input.TipoAsignacion!.Value,
            string.IsNullOrWhiteSpace(Input.Observaciones) ? null : Input.Observaciones.Trim());

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.CrearAsync(request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            PageFeedback.SetSuccess(
                TempData,
                $"La ocupación de {result.Value.PersonaNombre} en {result.Value.PuestoNombre} se creó correctamente.");

            return RedirectToPage("/Organizacion/Ocupaciones/Index");
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

                ErrorMessage = PageFeedback.UnauthorizedMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);
                return Page();
            }

            if (result.Error.Categoria == ErrorCategoria.NotFound)
            {
                ErrorMessage = PageFeedback.NotFoundDeleteMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);
                return Page();
            }

            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                // REQ-OCC-FORM-005: 409 debe discriminar entre
                // PersonaYPuestoOcupados (mapeo a ambos campos) y
                // PuestoOcupado (mapeo a PuestoId únicamente).
                MapConflictToModelState(result.Error);
                await ReloadFormStateAsync(cancellationToken);
                return Page();
            }

            if (result.Error.Categoria == ErrorCategoria.Validation
                && result.FieldErrors is { Count: > 0 })
            {
                ApplyFieldErrors(result.FieldErrors);
            }
            else
            {
                ErrorMessage = ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                    conflictMessage: "Conflicto al persistir la ocupación.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await ReloadFormStateAsync(cancellationToken);
        return Page();
    }

    private async Task ReloadFormStateAsync(CancellationToken cancellationToken)
    {
        await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);
        PuestoSinVacanteAbierta = Input.PuestoId is { } puestoId
            && puestoId != Guid.Empty
            && !await vacanteApiClient
                .ExisteVacanteAbiertaParaPuestoAsync(puestoId, cancellationToken)
                .ConfigureAwait(false);
    }
}
