using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel para la página Edit del módulo web de Puestos (PR 3B).
/// Carga el puesto por id más tres catálogos (unidades, cargos y puestos
/// para <c>PuestoSuperiorId</c>) en paralelo vía <c>Task.WhenAll</c>,
/// prepobla <see cref="IPuestoForm.Input"/> con los campos editables y
/// publica vía <see cref="IPuestosApiClient.UpdateAsync"/>. Tras éxito,
/// PRG-redirect hard-codeado a <c>/organizacion/puestos/detalles/{id}</c>
/// (la página Details llega en PR 3C). Sobre 409 mapea el mensaje a un
/// error general recuperable (no hay campo Codigo editable); sobre
/// <c>ValidationProblemDetails</c> aplica
/// <see cref="PuestoFormHelpers.ApplyFieldErrorsToModelState"/>.
/// Fallos de transporte se traducen a un error general recuperable y
/// conservan la entrada del usuario.
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

    /// <summary>
    /// GET handler. Carga el puesto por id y los tres catálogos en paralelo.
    /// Si el puesto no existe (<see cref="IPuestosApiClient.GetByIdAsync"/>
    /// devuelve <c>null</c>) o la consulta inicial falla, marca
    /// <see cref="IsRecoverable"/> y muestra un mensaje recuperable sin
    /// renderizar el formulario. Los parámetros <c>p</c>, <c>search</c>,
    /// <c>sort</c> y <c>status</c> se preservan para los enlaces de retorno.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "status")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;

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
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load edit page for puesto {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            return Page();
        }
    }

    /// <summary>
    /// POST handler. Valida ModelState; si pasa, arma un
    /// <see cref="ActualizarPuestoRequest"/> con los tres campos editables
    /// y llama <c>PUT /api/v1/puestos/{id}</c>. Sobre éxito, PRG hard-codeado
    /// a <c>/organizacion/puestos/detalles/{id}</c> (PR 3C refactoriza a
    /// <c>Url.Page</c>). Sobre 409 (CodigoDuplicado o PuestoSuperiorInvalido)
    /// mapea el mensaje a error general recuperable vía
    /// <see cref="PuestoPostResultMapper.TryMap"/>; sobre 400 con FieldErrors
    /// los aplica al ModelState. Cualquier fallo de transporte muestra error
    /// general y conserva input + catálogos.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "status")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;

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
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load puesto {Id} during POST prepopulate.", id);
            ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            await LoadCatalogsAsync(cancellationToken);
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
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
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

            // PRG a Details: hard-code del URL hasta que PR 3C introduzca la
            // página Details y podamos reemplazar por RedirectToPage("/Organizacion/Puestos/Details").
            return Redirect($"/organizacion/puestos/detalles/{id:D}");
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
        var unidadesTask = LaunchSafeAsync(() => unidadOrganizativaApiClient.QueryAsync(
            new UnidadOrganizativaListQuery(1, 200, null, null, "activas"),
            cancellationToken));
        var cargosTask = LaunchSafeAsync(() => cargoApiClient.GetAllAsync(cancellationToken));
        var puestosTask = LaunchSafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));

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
            PuestoSuperiorOptions = puestosTask.Result.Select(MapToSuperiorViewModel).ToArray();
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

    /// <summary>
    /// Envuelve una factory <c>() =&gt; Task&lt;T&gt;</c> capturando
    /// excepciones SINCRÓNICAS (las que algunos fakes lanzan antes de
    /// devolver un <c>Task.FromException</c>) y devolviendo un task
    /// faulted equivalente. Así <c>Task.WhenAll</c> puede consolidar
    /// éxitos y fallas de forma uniforme.
    /// </summary>
    private static Task<T> LaunchSafeAsync<T>(Func<Task<T>> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    private static PuestoListItemViewModel MapToSuperiorViewModel(PuestoDto dto)
        => new(dto.Id, dto.Codigo, dto.Nombre, dto.Descripcion, dto.UnidadOrganizativaNombre, dto.CargoNombre, dto.PuestoSuperiorId);
}