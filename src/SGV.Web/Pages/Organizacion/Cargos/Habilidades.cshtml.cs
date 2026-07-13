using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel de la grilla editable de habilidades por cargo. Permite al
/// <see cref="RolesSgv.Administrador"/> listar, asignar, actualizar y
/// quitar asociaciones <c>CargoHabilidad</c> consumiendo
/// <see cref="ICargoApiClient"/> para el subrecurso
/// <c>/api/v1/cargos/{id}/skills</c> y <see cref="IHabilidadApiClient"/>
/// para los catálogos de habilidades y niveles.
///
/// La página aplica <c>[Authorize]</c> a nivel de clase y un chequeo
/// explícito <c>User.IsInRole(RolesSgv.Administrador)</c> en cada
/// handler (alineado con el design del change). El chequeo explícito
/// evita depender de <c>[Authorize(Roles = ...)]</c> y mantiene la
/// respuesta <c>403 Forbidden</c> coherente con la frontera de admin que
/// aplica <c>CargosController</c> sobre los endpoints del subrecurso.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> en handlers de Delete. <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>. Se elimina el
/// filtro manual <c>IsTransportFailure</c> privado (que duplicaba la
/// lógica de <see cref="TransportFailureClassifier"/>) en favor del
/// helper centralizado.
/// </para>
/// </summary>
[Authorize]
public sealed class HabilidadesModel(
    ICargoApiClient cargoApiClient,
    IHabilidadApiClient habilidadApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<HabilidadesModel> logger) : PageModel
{
    /// <summary>
    /// Nombre del cargo que se muestra como encabezado de la grilla.
    /// <c>null</c> cuando la carga inicial falla con estado recuperable.
    /// </summary>
    public string? CargoNombre { get; private set; }

    /// <summary>
    /// Filas visibles en la grilla. Refleja la respuesta de
    /// <c>GET /api/v1/cargos/{cargoId}/skills</c>: cada item incluye
    /// <c>Skill</c>, <c>Nivel</c>, <c>SkillId</c>, <c>NivelRequeridoId</c>,
    /// <c>Ponderacion</c> y <c>EsObligatoria</c>.
    /// </summary>
    public IReadOnlyList<CargoSkillDetailDto> Skills { get; private set; } = [];

    /// <summary>
    /// Catálogo de habilidades activas para poblar el dropdown
    /// "Asignar nueva habilidad".
    /// </summary>
    public IReadOnlyList<HabilidadListItemViewModel> HabilidadesDisponibles { get; private set; } = [];

    /// <summary>
    /// Catálogo de niveles de habilidad para poblar el dropdown
    /// "NivelRequerido" del form de asignación y de cada fila editable.
    /// </summary>
    public IReadOnlyList<NivelHabilidadDto> NivelOptions { get; private set; } = [];

    /// <summary>
    /// Modelo ligado al form "Asignar nueva habilidad". NO lleva
    /// <c>[BindProperty]</c> a propósito: el binder por defecto de
    /// Razor Pages intentaría poblarlo desde CUALQUIER form key,
    /// incluidas las de la grilla con prefijo <c>Actualizar[xxx].</c>,
    /// generando entradas fantasma en ModelState con claves como
    /// <c>SkillId</c> y <c>NivelRequeridoId</c> que contaminan el
    /// handler de Actualizar. En lugar de eso, <see cref="OnPostAsignarAsync"/>
    /// lo hidrata explícitamente desde <c>Request.Form</c> usando los
    /// nombres <c>AsignarInput.Campo</c> del form de asignación (que
    /// sí son propios del flujo Asignar y no entran en conflicto con
    /// los nombres de la grilla). Ver nota en <see cref="OnPostActualizarAsync"/>
    /// sobre por qué no usar binding por convención para Actualizar.
    /// </summary>
    public CargoHabilidadAsignarInputModel AsignarInput { get; set; } = new();

    /// <summary>
    /// Mensaje de error visible cuando la carga inicial o un POST fallido
    /// no pueden propagarse vía TempData (e.g., failure de transporte).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// <c>true</c> cuando el cargo no existe o la consulta inicial falla;
    /// la vista muestra un mensaje recuperable y oculta la grilla + el
    /// form. </summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>
    /// Mensaje de feedback (success/warning/danger) que llega vía TempData
    /// tras un PRG desde un POST handler.
    /// </summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>
    /// Tipo de feedback del <see cref="StatusMessage"/>. Default
    /// <c>"success"</c> si no hay TempData poblado.
    /// </summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>
    /// <c>true</c> cuando el usuario autenticado pertenece al rol
    /// <see cref="RolesSgv.Administrador"/>. Se usa tanto en los
    /// handlers (chequeo previo a invocar el cliente API) como en el
    /// markup para ocultar acciones de escritura cuando el principal
    /// no tiene el rol.
    /// </summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    // ──────────────────────────────────────────────
    // GET
    // ──────────────────────────────────────────────

    /// <summary>
    /// Carga el cargo + skills + catálogos. Si el rol no es admin,
    /// devuelve <see cref="ForbidResult"/> (403). Si el cargo no existe
    /// o la consulta falla de manera recuperable, marca
    /// <see cref="IsRecoverable"/> y re-renderiza con un mensaje
    /// accionable.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        try
        {
            var cargo = await cargoApiClient.GetByIdAsync(id, cancellationToken);
            if (cargo is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El cargo solicitado no está disponible.";
                logger.LogWarning("Cargo with Id {CargoId} was not found or is no longer available.", id);
                return Page();
            }

            CargoNombre = cargo.Nombre;
            await LoadSkillsAndCatalogsAsync(id, cancellationToken);
            return Page();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load habilidades page for cargo {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar la página de habilidades. Intentá nuevamente.";
            return Page();
        }
    }

    // ──────────────────────────────────────────────
    // POST Asignar (Req 3 escenario "Asignar una nueva habilidad")
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnPostAsignarAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        AsignarInput = CargoSkillFormHelpers.ReadAsignarInput(Request.Form, ModelState);

        if (!ModelState.IsValid)
        {
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        var request = new AsignarCargoSkillRequest(
            AsignarInput.NivelRequeridoId!.Value,
            AsignarInput.Ponderacion,
            AsignarInput.EsObligatoria);

        CargoSkillCommandResult result;
        try
        {
            result = await cargoApiClient.UpsertSkillAsync(id, AsignarInput.SkillId!.Value, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill upsert transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "La habilidad se asignó correctamente al cargo.");
            return RedirectToPage(new { id });
        }

        ErrorMessage = CargoSkillFormHelpers.ApplyAsignarFailureToModelState(result, ModelState);
        await ReloadForFailureAsync(id, cancellationToken);
        return Page();
    }

    // ──────────────────────────────────────────────
    // POST Actualizar (Req 3 escenario "Editar una habilidad existente")
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnPostActualizarAsync(
        Guid id,
        Guid skillId,
        CancellationToken cancellationToken)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        if (!CargoSkillFormHelpers.TryReadActualizarRequest(skillId, Request.Form, ModelState, out var request))
        {
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        CargoSkillCommandResult result;
        try
        {
            result = await cargoApiClient.UpsertSkillAsync(id, skillId, request!, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "La habilidad del cargo se actualizó correctamente.");
            return RedirectToPage(new { id });
        }

        ErrorMessage = CargoSkillFormHelpers.ApplyActualizarFailureToModelState(skillId, result, ModelState);
        await ReloadForFailureAsync(id, cancellationToken);
        return Page();
    }

    // ──────────────────────────────────────────────
    // POST Quitar (Req 4)
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnPostQuitarAsync(Guid id, Guid skillId, CancellationToken cancellationToken)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        CargoSkillDeleteResult result;
        try
        {
            result = await cargoApiClient.DeleteSkillAsync(id, skillId, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill delete transport failure.");
            PageFeedback.SetDanger(TempData, "No se pudo contactar al servicio de habilidades. Intentá nuevamente.");
            return RedirectToPage(new { id });
        }

        if (result.Succeeded)
        {
            PageFeedback.SetSuccess(TempData, "La habilidad se quitó del cargo correctamente.");
            return RedirectToPage(new { id });
        }

        // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
        if (result.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        // 404 al quitar = la asociación ya no existe. Reflejo del estado
        // real (probable race con otra pestaña / un refresh sobre una fila
        // stale). No es un error fatal: redirigimos con TempData warning
        // para que el siguiente GET refresque la grilla.
        if (result.Categoria == ErrorCategoria.NotFound)
        {
            PageFeedback.SetWarning(TempData, "La asociación ya no existe. La grilla fue actualizada.");
            return RedirectToPage(new { id });
        }

        var failureMessage = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : MapCategoriaToMessage(result.Categoria);
        PageFeedback.SetDanger(TempData, failureMessage);
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Switch exhaustivo sobre <see cref="ErrorCategoria"/>. Cubre las 7
    /// variantes sin <c>default</c> silencioso (design §8.1, F3).
    /// <c>Unauthorized</c> lanza porque su flujo es redirigir vía
    /// <see cref="IAuthSessionRedirector"/> antes de mostrar mensaje inline.
    /// </summary>
    internal static string MapCategoriaToMessage(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => PageFeedback.NotFoundDeleteMessage,
        ErrorCategoria.Conflict => "Conflicto al procesar la operación.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => PageFeedback.UnauthorizedMessage,
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

    // ──────────────────────────────────────────────
    // Helpers privados
    // ──────────────────────────────────────────────

    /// <summary>
    /// Carga skills + catálogos en paralelo. Si el catálogo falla, no
    /// tiramos la página: registramos el error y dejamos los dropdowns
    /// vacíos para que el form sea re-presentable.
    /// </summary>
    private async Task LoadSkillsAndCatalogsAsync(Guid cargoId, CancellationToken cancellationToken)
    {
        try
        {
            var skillsTask = cargoApiClient.GetSkillsAsync(cargoId, cancellationToken);
            var nivelesTask = habilidadApiClient.GetNivelesHabilidadAsync(cancellationToken);
            var habilidadesTask = habilidadApiClient.GetAllAsync(cancellationToken);

            await Task.WhenAll(skillsTask, nivelesTask, habilidadesTask);

            Skills = skillsTask.Result;
            NivelOptions = nivelesTask.Result;
            HabilidadesDisponibles = habilidadesTask.Result
                .Select(h => new HabilidadListItemViewModel(h.Id, h.Codigo, h.Nombre, h.Descripcion, h.Categoria))
                .ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load catalogs for habilidades page (cargoId={CargoId}).", cargoId);
            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = "No se pudo cargar el catálogo de habilidades o niveles.";
            }
        }
    }

    /// <summary>
    /// Recarga los datos necesarios para re-renderizar la página tras un
    /// fallo de POST o validación. A diferencia de
    /// <see cref="LoadSkillsAndCatalogsAsync"/>, esta ruta también
    /// garantiza <see cref="CargoNombre"/> cuando el form se re-renderiza
    /// desde un POST handler (la página no recibió OnGetAsync).
    /// </summary>
    private async Task ReloadForFailureAsync(Guid id, CancellationToken cancellationToken)
    {
        CargoNombre ??= await TryLoadCargoNombreAsync(id, cancellationToken);
        await LoadSkillsAndCatalogsAsync(id, cancellationToken);
    }

    private async Task<string?> TryLoadCargoNombreAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var cargo = await cargoApiClient.GetByIdAsync(id, cancellationToken);
            return cargo?.Nombre;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(ex, "Failed to refresh cargo nombre during POST reload.");
            return null;
        }
    }

}