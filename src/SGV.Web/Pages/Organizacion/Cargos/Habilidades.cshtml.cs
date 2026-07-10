using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Text.Json;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Seguridad;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;

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
/// </summary>
[Authorize]
public sealed class HabilidadesModel(
    ICargoApiClient cargoApiClient,
    IHabilidadApiClient habilidadApiClient,
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
    public string? StatusMessage => TempData["StatusMessage"] as string;

    /// <summary>
    /// Tipo de feedback del <see cref="StatusMessage"/>. Default
    /// <c>"success"</c> si no hay TempData poblado.
    /// </summary>
    public string StatusKind => TempData["StatusKind"] as string ?? "success";

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
        catch (Exception ex) when (IsTransportFailure(ex))
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

        // Hidratamos AsignarInput manualmente desde Request.Form (ver
        // nota en la propiedad sobre por qué no usamos [BindProperty]
        // a nivel de clase). Esto evita la interferencia con las keys
        // indexadas Actualizar[xxx].Campo de la grilla editable.
        var skillIdRaw = Request.Form["AsignarInput.SkillId"].ToString();
        var nivelRaw = Request.Form["AsignarInput.NivelRequeridoId"].ToString();
        var ponderacionRaw = Request.Form["AsignarInput.Ponderacion"].ToString();
        var esObligatoriaRaw = Request.Form["AsignarInput.EsObligatoria"].ToString();

        if (!Guid.TryParse(skillIdRaw, out var skillId) || skillId == Guid.Empty)
        {
            ModelState.AddModelError("AsignarInput.SkillId", "Debe seleccionar una habilidad.");
        }

        if (!Guid.TryParse(nivelRaw, out var nivelId) || nivelId == Guid.Empty)
        {
            ModelState.AddModelError("AsignarInput.NivelRequeridoId", "Debe seleccionar un nivel requerido.");
        }

        if (!decimal.TryParse(ponderacionRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var ponderacion)
            || ponderacion < 0.01m
            || ponderacion > 100.00m)
        {
            ModelState.AddModelError("AsignarInput.Ponderacion", "La ponderación debe estar entre 0,01 y 100,00.");
        }

        var esObligatoria = string.Equals(esObligatoriaRaw, "true", StringComparison.OrdinalIgnoreCase);

        // Sincronizamos la propiedad pública para que el helper
        // ApplyAsignarFailureToModelState siga encontrando los datos
        // que ya validamos, y para que el form re-renderizado pueda
        // mostrar los valores enviados.
        AsignarInput.SkillId = skillId == Guid.Empty ? null : skillId;
        AsignarInput.NivelRequeridoId = nivelId == Guid.Empty ? null : nivelId;
        AsignarInput.Ponderacion = decimal.TryParse(ponderacionRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
        AsignarInput.EsObligatoria = esObligatoria;

        if (!ModelState.IsValid)
        {
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        // ModelState.IsValid garantiza SkillId y NivelRequeridoId no nulos.
        // El operador ! es seguro en este punto.
        var request = new AsignarCargoSkillRequest(
            AsignarInput.NivelRequeridoId!.Value,
            AsignarInput.Ponderacion,
            AsignarInput.EsObligatoria);

        CargoSkillCommandResult result;
        try
        {
            result = await cargoApiClient.UpsertSkillAsync(id, AsignarInput.SkillId!.Value, request, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill upsert transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        if (result.IsSuccess)
        {
            TempData["StatusMessage"] = "La habilidad se asignó correctamente al cargo.";
            TempData["StatusKind"] = "success";
            return RedirectToPage(new { id });
        }

        ApplyAsignarFailureToModelState(result);
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

        // El markup de la grilla nombra los inputs con la convención
        // indexada Actualizar[{skillId}].Campo (alineada con design.md
        // sección 4). El binder por defecto de Razor Pages no puede
        // popular un parámetro fuertemente tipado a partir de un prefijo
        // Guid-indexado: o intenta tratarlo como un Dictionary de la
        // propiedad AsignarInput (generando entradas fantasma en
        // ModelState con claves como "SkillId" y "NivelRequeridoId"), o
        // requiere atributos [Bind] que no aplican a nivel de propiedad
        // en este PageModel. Por eso extraemos manualmente desde
        // Request.Form con el prefijo del skill activo, validamos en
        // línea y construimos el request del cliente API. Los errores
        // van bajo la MISMA convención indexada para que el contenedor
        // per-row del markup los muestre.
        var nivelFormKey = $"Actualizar[{skillId}].NivelRequeridoId";
        var ponderacionFormKey = $"Actualizar[{skillId}].Ponderacion";
        var esObligatoriaFormKey = $"Actualizar[{skillId}].EsObligatoria";

        var nivelRaw = Request.Form[nivelFormKey].ToString();
        var ponderacionRaw = Request.Form[ponderacionFormKey].ToString();
        var esObligatoriaRaw = Request.Form[esObligatoriaFormKey].ToString();

        if (!Guid.TryParse(nivelRaw, out var nivelId) || nivelId == Guid.Empty)
        {
            ModelState.AddModelError(nivelFormKey, "Debe seleccionar un nivel requerido.");
        }

        if (!decimal.TryParse(ponderacionRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var ponderacion)
            || ponderacion < 0.01m
            || ponderacion > 100.00m)
        {
            ModelState.AddModelError(ponderacionFormKey, "La ponderación debe estar entre 0,01 y 100,00.");
        }

        // El checkbox no es obligatorio desde el punto de vista de
        // ModelState (default false = "no obligatoria" según
        // CargoHabilidadActualizarInputModel). Si no llega al form,
        // interpretamos canónicamente como false.
        var esObligatoria = string.Equals(esObligatoriaRaw, "true", StringComparison.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
        {
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        var request = new AsignarCargoSkillRequest(
            nivelId,
            ponderacion,
            esObligatoria);

        CargoSkillCommandResult result;
        try
        {
            result = await cargoApiClient.UpsertSkillAsync(id, skillId, request, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await ReloadForFailureAsync(id, cancellationToken);
            return Page();
        }

        if (result.IsSuccess)
        {
            TempData["StatusMessage"] = "La habilidad del cargo se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage(new { id });
        }

        ApplyActualizarFailureToModelState(skillId, result);
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
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Cargo skill delete transport failure.");
            TempData["StatusMessage"] = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            TempData["StatusKind"] = "danger";
            return RedirectToPage(new { id });
        }

        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "La habilidad se quitó del cargo correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage(new { id });
        }

        // 404 al quitar = la asociación ya no existe. Reflejo del estado
        // real (probable race con otra pestaña / un refresh sobre una fila
        // stale). No es un error fatal: redirigimos con TempData warning
        // para que el siguiente GET refresque la grilla.
        if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            TempData["StatusMessage"] = "La asociación ya no existe. La grilla fue actualizada.";
            TempData["StatusKind"] = "warning";
            return RedirectToPage(new { id });
        }

        var failureMessage = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : "No se pudo quitar la habilidad del cargo.";
        TempData["StatusMessage"] = failureMessage;
        TempData["StatusKind"] = "danger";
        return RedirectToPage(new { id });
    }

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
        catch (Exception ex) when (IsTransportFailure(ex))
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
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogWarning(ex, "Failed to refresh cargo nombre during POST reload.");
            return null;
        }
    }

    /// <summary>
    /// Traduce un <see cref="CargoSkillCommandResult"/> de fallo del handler
    /// <c>OnPostAsignarAsync</c> a <see cref="ModelState"/>. Los errores por
    /// campo se prefijan con <c>AsignarInput.</c> para que el
    /// <c>asp-validation-for</c> del form de asignación los muestre junto
    /// al input correcto. Esta variante NO se usa desde
    /// <c>OnPostActualizarAsync</c>: para esa ruta usamos
    /// <see cref="ApplyActualizarFailureToModelState"/>.
    /// </summary>
    private void ApplyAsignarFailureToModelState(CargoSkillCommandResult result)
    {
        if (result.FieldErrors is { Count: > 0 })
        {
            foreach (var kvp in result.FieldErrors)
            {
                var key = kvp.Key.StartsWith("AsignarInput.", StringComparison.OrdinalIgnoreCase)
                    ? kvp.Key
                    : "AsignarInput." + kvp.Key;
                foreach (var fieldMessage in kvp.Value)
                {
                    ModelState.AddModelError(key, fieldMessage);
                }
            }
            return;
        }

        if (result.Error is null)
        {
            return;
        }

        var message = result.Error.Message;
        switch (result.Error.Type)
        {
            case CargoSkillErrorType.NotFound:
                ModelState.AddModelError(string.Empty, "El cargo o la habilidad solicitada no existe.");
                break;
            case CargoSkillErrorType.Conflict:
                ModelState.AddModelError(string.Empty, message);
                break;
            case CargoSkillErrorType.Forbidden:
                ErrorMessage = "No tiene permisos para modificar las habilidades del cargo.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            case CargoSkillErrorType.Unauthorized:
                ErrorMessage = "Su sesión expiró. Vuelva a iniciar sesión.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            case CargoSkillErrorType.Transport:
                ErrorMessage = "El servicio no respondió correctamente. Intentá nuevamente.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            default:
                ErrorMessage = message;
                ModelState.AddModelError(string.Empty, message);
                break;
        }
    }

    /// <summary>
    /// Whitelist de campos del vínculo <c>CargoHabilidad</c> que pueden
    /// aparecer en <c>FieldErrors</c> del backend y a los que tiene sentido
    /// anclar el mensaje a una fila específica de la grilla editable. Las
    /// claves que no pertenezcan a este whitelist caen al summary general
    /// (caso defensivo) sin anclaje a fila.
    /// </summary>
    private static readonly HashSet<string> ActualizarFieldWhitelist =
        new(StringComparer.OrdinalIgnoreCase) { "NivelRequeridoId", "Ponderacion", "EsObligatoria" };

    /// <summary>
    /// Traduce un <see cref="CargoSkillCommandResult"/> de fallo del handler
    /// <c>OnPostActualizarAsync</c> a <see cref="ModelState"/> anclando los
    /// errores a la fila activa identificada por <paramref name="skillId"/>.
    /// Para cada <c>FieldErrors["Campo"]</c> con <c>Campo</c> en el whitelist
    /// <see cref="ActualizarFieldWhitelist"/>, se agrega el mensaje bajo la
    /// key <c>Actualizar[{skillId}].Campo</c> para que el contenedor de
    /// error de la fila lo muestre junto al input correspondiente; el mismo
    /// mensaje también se vuelca a <c>string.Empty</c> para que el
    /// <c>asp-validation-summary</c> general lo presente. Las claves fuera
    /// del whitelist caen únicamente a <c>string.Empty</c> para mantener
    /// visible el error sin anclaje a fila.
    /// </summary>
    private void ApplyActualizarFailureToModelState(Guid skillId, CargoSkillCommandResult result)
    {
        if (result.FieldErrors is { Count: > 0 })
        {
            foreach (var kvp in result.FieldErrors)
            {
                var isWhitelisted = ActualizarFieldWhitelist.Contains(kvp.Key);
                foreach (var fieldMessage in kvp.Value)
                {
                    if (isWhitelisted)
                    {
                        ModelState.AddModelError($"Actualizar[{skillId}].{kvp.Key}", fieldMessage);
                    }
                    // Tanto las claves whitelisted como las defensivas caen al
                    // summary general (key vacía) para que el
                    // asp-validation-summary siga mostrando el mensaje.
                    ModelState.AddModelError(string.Empty, fieldMessage);
                }
            }
            return;
        }

        if (result.Error is null)
        {
            return;
        }

        var message = result.Error.Message;
        switch (result.Error.Type)
        {
            case CargoSkillErrorType.NotFound:
                ModelState.AddModelError(string.Empty, "El cargo o la habilidad solicitada no existe.");
                break;
            case CargoSkillErrorType.Conflict:
                ModelState.AddModelError(string.Empty, message);
                break;
            case CargoSkillErrorType.Forbidden:
                ErrorMessage = "No tiene permisos para modificar las habilidades del cargo.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            case CargoSkillErrorType.Unauthorized:
                ErrorMessage = "Su sesión expiró. Vuelva a iniciar sesión.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            case CargoSkillErrorType.Transport:
                ErrorMessage = "El servicio no respondió correctamente. Intentá nuevamente.";
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
            default:
                ErrorMessage = message;
                ModelState.AddModelError(string.Empty, message);
                break;
        }
    }

    /// <summary>
    /// Clasifica una excepción como falla de transporte para el cliente
    /// HTTP. NO incluye <see cref="OperationCanceledException"/> cuando
    /// el <see cref="CancellationToken"/> del request ya está cancelado:
    /// dejamos que la cancelación cooperativa suba al pipeline para
    /// evitar renderizar sobre un cliente desconectado.
    /// </summary>
    private bool IsTransportFailure(Exception ex) =>
        ex is HttpRequestException ||
        ex is TaskCanceledException ||
        ex is JsonException;
}