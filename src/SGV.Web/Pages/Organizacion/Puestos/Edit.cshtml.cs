using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel de Edit del módulo Puestos (PR 3B). Maneja los cuatro caminos
/// del POST: <c>Success</c> (PRG a Details), <c>FieldErrors</c> (vía
/// <see cref="PuestoFormHelpers.ApplyFieldErrorsToModelState"/>),
/// <c>Conflict</c> (mensaje general recuperable, no hay campo Codigo
/// editable) y <c>HttpFailure</c> (error general que conserva input + catálogos).
/// El pre-populate del GET/POST es un workaround para los <c>[Required]</c>
/// heredados de Create en los campos inmutables (Codigo/UnidadOrganizativaId/
/// CargoId) que el form de Edit NO renderiza.
/// </summary>
[Authorize]
public sealed class EditModel(
    IPuestosApiClient puestosApiClient,
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ICargoApiClient cargoApiClient,
    ILogger<EditModel> logger) : PageModel, IPuestoForm
{
    [BindProperty]
    public PuestoInputModel Input { get; set; } = new();

    public IReadOnlyList<UnidadOrganizativaDto> UnidadOrganizativaOptions { get; private set; } = [];

    public IReadOnlyList<CargoDto> CargoOptions { get; private set; } = [];

    public IReadOnlyList<PuestoListItemViewModel> PuestoSuperiorOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// Indica si el puesto solicitado no pudo cargarse (404 o error de
    /// transporte). En ese estado la vista muestra un mensaje
    /// recuperable y oculta el formulario.
    /// </summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>Mensaje de estado (success/warning) que llega vía TempData tras un PRG.</summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public string? ReturnStatus { get; set; }

    public string ReturnToListUrl => PuestoFormHelpers.BuildReturnToListUrl(
        Url,
        ReturnPage,
        ReturnSearch,
        ReturnSort,
        ReturnStatus);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    private void CaptureReturnContext(
        string? p, string? search, string? sort, string? returnStatus)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(returnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;
    }

    /// <summary>
    /// GET handler. Si el puesto no existe (<see cref="IPuestosApiClient.GetByIdAsync"/>
    /// devuelve <c>null</c>) o falla el transporte, marca
    /// <see cref="IsRecoverable"/> y muestra un mensaje recuperable sin
    /// renderizar el formulario. Los parámetros <c>p</c>, <c>search</c>,
    /// <c>sort</c> y <c>returnStatus</c> se preservan para los enlaces de
    /// retorno (paridad con <c>Puestos/Details</c>, que también bindea
    /// <c>returnStatus</c>; el helper <c>BuildEditRouteValues</c> del Index
    /// emite este mismo nombre).
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            // Patrón canónico del repo (ver Habilidades.cshtml.cs): Forbid()
            // delega al cookie scheme, que redirige a AccessDeniedPath
            // ("/error/403" configurado en Program.cs). Es testeable y
            // simétrico con el POST handler de este mismo PageModel.
            return Forbid();
        }

        CaptureReturnContext(p, search, sort, returnStatus);

        try
        {
            var puesto = await puestosApiClient.GetByIdAsync(id, cancellationToken);
            if (puesto is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El puesto solicitado no está disponible.";
                logger.LogWarning("Puesto with Id {PuestoId} was not found or is no longer available.", id);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            // Prepopula los tres campos editables; los inmutables quedan en
            // su valor por defecto (Codigo="", UnidadOrganizativaId=null,
            // CargoId=null) y NO se renderizan en el HTML porque el partial
            // _Form.cshtml oculta esos inputs cuando IsEdit=true.
            Input.Nombre = puesto.Nombre;
            Input.Descripcion = puesto.Descripcion;
            Input.PuestoSuperiorId = puesto.PuestoSuperiorId;

            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load edit page for puesto {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            return Page();
        }
    }

    /// <summary>
    /// POST handler. Maneja los 4 caminos: <c>Success</c> (PRG hard-code a
    /// <c>/organizacion/puestos/detalles/{id}</c> hasta PR 3C),
    /// <c>FieldErrors</c> (vía <see cref="PuestoPostResultMapper.TryMap"/>),
    /// <c>Conflict</c> (mensaje general recuperable) y <c>HttpFailure</c>
    /// (error general que conserva input + catálogos).
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        CaptureReturnContext(p, search, sort, returnStatus);

        // Pre-poblar los campos inmutables (Codigo, UnidadOrganizativaId,
        // CargoId) desde el DTO antes de validar ModelState. Razón: estos
        // campos tienen [Required] en PuestoInputModel para que Create los
        // valide, pero Edit NO los renderiza en el form (decisión locked
        // #3 — son inmutables en un Puesto existente), por lo que el POST
        // no los envía y ModelState.IsValid sería false. Recuperarlos del
        // API preserva el contrato del modelo compartido sin filtrar
        // inputs del usuario al backend (ActualizarPuestoRequest sólo
        // recibe los 3 campos editables).
        try
        {
            var current = await puestosApiClient.GetByIdAsync(id, cancellationToken);
            if (current is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El puesto solicitado no está disponible.";
                logger.LogWarning("Puesto with Id {PuestoId} was not found during POST.", id);
                return Page();
            }
            Input.Codigo = current.Codigo;
            Input.UnidadOrganizativaId = current.UnidadOrganizativaId;
            Input.CargoId = current.CargoId;
            // Limpia los errores de ModelState que el binder agregó por los
            // [Required] de los campos inmutables: ya están poblados desde
            // el DTO y NO vienen del form (el form de Edit no los renderiza).
            ModelState.Remove(PuestoFormKeys.CodigoKey);
            ModelState.Remove(PuestoFormKeys.UnidadOrganizativaIdKey);
            ModelState.Remove(PuestoFormKeys.CargoIdKey);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load puesto {Id} during POST prepopulate.", id);
            ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            // LoadCatalogsAsync arranca con ErrorMessage = null y sólo lo
            // restaura si alguna llamada falla. Si los catálogos responden
            // OK, pisa nuestro mensaje; preservamos el valor de pre-populate
            // y lo re-asignamos si quedó vacío.
            var preservedError = ErrorMessage;
            await LoadCatalogsAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = preservedError;
            }
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new ActualizarPuestoRequest(
            Input.Nombre,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim(),
            Input.PuestoSuperiorId);

        PuestoCommandResult result;
        try
        {
            result = await puestosApiClient.UpdateAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed body).
            // Map to a recoverable error: keep user input, reload the catalog,
            // re-render the page so the user can retry.
            logger.LogError(ex, "Puesto update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de puestos. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[nameof(StatusMessage)] = $"El puesto \"{result.Value.Nombre}\" se actualizó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            // PR 3C — refactor del PRG a Details. Antes la página Details no
            // existía, por lo que el PRG usaba un hard-code del URL. Ahora que
            // la página existe (PR 3C), usamos RedirectToPage que resuelve el
            // URL a través del routing y propaga el contexto de navegación
            // (p/search/sort/returnStatus) para que el Details pueda mostrar el
            // link "Volver al listado" preservando el origen.
            return RedirectToPage("/Organizacion/Puestos/Details", new
            {
                id,
                p,
                search,
                sort,
                returnStatus
            });
        }

        if (result.Error is not null)
        {
            // 409 (CodigoDuplicado / PuestoSuperiorInvalido) → no podemos
            // mapear a un campo específico porque Codigo no es editable y
            // PuestoSuperiorInvalido cae bajo la key del campo pero el
            // contrato backend puede variar. Fallback: mapper que aplica
            // FieldErrors si los hay, o mensaje general bajo string.Empty.
            if (!PuestoPostResultMapper.TryMap(result, ModelState))
            {
                ErrorMessage = result.Error.Message;
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Carga los tres catálogos en paralelo vía <c>Task.WhenAll</c>.
    /// Cualquier excepción (sincrónica o asincrónica) de uno o más
    /// catálogos se registra con <see cref="ErrorMessage"/> y el catálogo
    /// correspondiente queda vacío. El form sigue visible para permitir
    /// reintento manual. El helper <see cref="LaunchSafeAsync"/> convierte
    /// throws sincrónicos en faulted tasks (mismo workaround que Create).
    /// </summary>
    private async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        // Las unidades y cargos se cargan aunque sean inmutables en Edit
        // (paridad con Create: el dropdown de PuestoSuperiorId los referencia
        // vía CodigoYNombre). Si una falla, los demás siguen disponibles.
        var unidadesTask = PuestoFormHelpers.LaunchSafeAsync(() => unidadOrganizativaApiClient.QueryAsync(
            new UnidadOrganizativaListQuery(1, 200, null, null, "activas"),
            cancellationToken));
        var cargosTask = PuestoFormHelpers.LaunchSafeAsync(() => cargoApiClient.GetAllAsync(cancellationToken));
        var puestosTask = PuestoFormHelpers.LaunchSafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));

        try
        {
            await Task.WhenAll(unidadesTask, cargosTask, puestosTask);
        }
        catch
        {
            // Task.WhenAll throws on the first faulted task. Capturamos
            // localmente y consolidamos el estado de cada catálogo por
            // separado vía Task.Status a continuación.
        }

        if (unidadesTask.Status == TaskStatus.RanToCompletion)
        {
            UnidadOrganizativaOptions = unidadesTask.Result.Items;
        }
        else
        {
            UnidadOrganizativaOptions = [];
            anyFailure = true;
        }

        if (cargosTask.Status == TaskStatus.RanToCompletion)
        {
            CargoOptions = cargosTask.Result;
        }
        else
        {
            CargoOptions = [];
            anyFailure = true;
        }

        if (puestosTask.Status == TaskStatus.RanToCompletion)
        {
            // Mapea DTO → view model (igual que Create). El puesto actual puede
            // no aparecer en la lista si no está sembrado en GetAllResult; el
            // <option selected> del helper asp-for de Razor lo mantiene visible.
            PuestoSuperiorOptions = puestosTask.Result.Select(PuestoFormHelpers.MapToSuperiorViewModel).ToArray();
        }
        else
        {
            PuestoSuperiorOptions = [];
            anyFailure = true;
        }

        if (anyFailure)
        {
            ErrorMessage = "No se pudo cargar el catálogo necesario. Intentá nuevamente.";
        }
    }
}
