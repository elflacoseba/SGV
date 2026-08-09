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
/// <para>
/// T2.8 (change <c>invertir-flujo-cubrir</c> / S2): si el GET trae
/// <c>?vacanteId={guid}</c>, la página resuelve la Vacante vía
/// <see cref="IVacanteApiClient.ObtenerPorIdAsync"/>. Abierta/En Selección
/// precargan <see cref="OcupacionInputModel.PuestoId"/> y muestran el
/// hint FORM-001; Cubierta/Cancelada/Inexistente muestran error legible
/// sin renderear el form.
/// </para>
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
    /// WU-4 (PR #259 review H-6): cache del resultado de
    /// <see cref="IVacanteApiClient.ExisteVacanteAbiertaParaPuestoAsync"/>
    /// para no repetir la consulta en <see cref="OnGetAsync"/> y
    /// <see cref="ReloadFormStateAsync"/>. <c>null</c> = sin cache.
    /// </summary>
    private bool? _puestoTieneVacanteCache;

    /// <summary>
    /// T2.10 (change <c>invertir-flujo-cubrir</c> / S2): <c>override</c>
    /// de la propiedad base para que cuando el GET trae
    /// <c>?vacanteId=</c> con Vacante Abierta/En Selección, el dropdown
    /// se renderee bloqueado. El setter es <c>protected</c> en la base;
    /// acá se expone como <c>private set</c> para que sólo el PageModel
    /// lo controle (no es editable desde la vista).
    /// </summary>
    public override bool PuestoIdBloqueadoPorVacante { get; protected set; }

    /// <summary>
    /// GET handler. Pre-carga <see cref="OcupacionInputModel.PersonaId"/>
    /// y <see cref="OcupacionInputModel.PuestoId"/> desde el query string
    /// (paridad con la página cruzada <c>PersonaOcupaciones</c> de Slice 3b)
    /// y carga los catálogos Persona/Puesto en paralelo.
    /// T2.8: si viene <c>?vacanteId=</c> resuelve la Vacante y precarga
    /// los campos del form con el flujo Cubrir invertido.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "personaId")] Guid? personaId = null,
        [FromQuery(Name = "puestoId")] Guid? puestoId = null,
        [FromQuery(Name = "vacanteId")] Guid? vacanteId = null,
        CancellationToken cancellationToken = default)
    {
        Input.PersonaId ??= personaId;
        Input.PuestoId ??= puestoId;

        await LoadCatalogsAsync(personaApiClient, puestosApiClient, logger, cancellationToken);

        // T2.8: si el GET trae ?vacanteId=, el flujo Cubrir tiene
        // precedencia sobre ?puestoId= (mismo form, distinto entry point).
        // Validar el estado de la Vacante y renderear el form con
        // PuestoId resuelto y bloqueado, o cortar con error legible.
        if (vacanteId.HasValue && vacanteId.Value != Guid.Empty)
        {
            return await ResolverVacanteParaCrearAsync(vacanteId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        // T-7.1: si el Puesto ya viene precargado, consultamos si tiene
        // Vacante abierta para mostrar el hint correcto. WU-4 guarda
        // el resultado para reusarlo en ReloadFormStateAsync.
        if (Input.PuestoId.HasValue && Input.PuestoId.Value != Guid.Empty)
        {
            var tieneVacante = await vacanteApiClient
                .ExisteVacanteAbiertaParaPuestoAsync(Input.PuestoId.Value, cancellationToken)
                .ConfigureAwait(false);
            _puestoTieneVacanteCache = tieneVacante;
            PuestoSinVacanteAbierta = !tieneVacante;
        }

        return Page();
    }

    /// <summary>
    /// T2.8: resuelve la Vacante vía <see cref="IVacanteApiClient.ObtenerPorIdAsync"/>
    /// y renderea el form según su estado. Abierta/En Selección →
    /// <see cref="Input"/> precargado con <see cref="OcupacionInputModel.VacanteId"/>
    /// y <see cref="OcupacionInputModel.PuestoId"/> y dropdown bloqueado;
    /// Cubierta/Cancelada → error legible sin form; inexistente → error
    /// legible sin form.
    /// </summary>
    private async Task<IActionResult> ResolverVacanteParaCrearAsync(
        Guid vacanteId,
        CancellationToken cancellationToken)
    {
        var vacante = await vacanteApiClient
            .ObtenerPorIdAsync(vacanteId, cancellationToken)
            .ConfigureAwait(false);

        if (vacante is null)
        {
            ErrorMessage = "La Vacante no existe.";
            return Page();
        }

        if (string.Equals(vacante.EstadoVacanteNombre, "Cubierta", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Esta Vacante ya está cubierta.";
            return Page();
        }

        if (string.Equals(vacante.EstadoVacanteNombre, "Cancelada", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Esta Vacante está cancelada y no puede cubrirse.";
            return Page();
        }

        // Estado Abierta o En Selección: renderear form con PuestoId
        // resuelto y bloqueado, hint informativo FORM-009 invertido.
        Input.VacanteId = vacante.Id;
        Input.PuestoId = vacante.PuestoId;
        PuestoIdBloqueadoPorVacante = true;
        PuestoSinVacanteAbierta = false;
        _puestoTieneVacanteCache = true;
        VacanteHintLabel =
            $"Esta Vacante del Puesto {vacante.PuestoNombre} se cubrirá al enviar "
            + "el formulario. La Ocupación creada transitará la Vacante a Cubierta "
            + "en la misma transacción.";
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
    /// T2.8: si el form trae <c>VacanteId</c>, lo propaga al
    /// <see cref="CrearOcupacionRequest"/> para el flujo Cubrir.
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
            string.IsNullOrWhiteSpace(Input.Observaciones) ? null : Input.Observaciones.Trim(),
            Input.VacanteId);

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

            // T2.8: si el alta provenía de ?vacanteId=, redirigir al
            // detalle de la Vacante para que el admin vea la transición
            // a Cubierta y la Ocupación derivada.
            if (Input.VacanteId.HasValue && Input.VacanteId.Value != Guid.Empty)
            {
                return RedirectToPage("/Organizacion/Vacantes/Details", new { id = Input.VacanteId.Value });
            }

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
        if (Input.PuestoId is not { } puestoId || puestoId == Guid.Empty)
        {
            PuestoSinVacanteAbierta = false;
            return;
        }

        // WU-4: reusar el cache de OnGetAsync si está disponible para
        // evitar un round-trip extra a la API. Sólo re-consultar ante
        // cambio de Puesto (cache stale) o primera entrada vía POST.
        if (_puestoTieneVacanteCache is { } cached)
        {
            PuestoSinVacanteAbierta = !cached;
            return;
        }

        var tieneVacante = await vacanteApiClient
            .ExisteVacanteAbiertaParaPuestoAsync(puestoId, cancellationToken)
            .ConfigureAwait(false);
        _puestoTieneVacanteCache = tieneVacante;
        PuestoSinVacanteAbierta = !tieneVacante;
    }
}
