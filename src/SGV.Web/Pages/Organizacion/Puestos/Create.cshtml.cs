using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel para la página Create del módulo web de Puestos (PR 3A).
/// Carga los tres catálogos necesarios (unidades organizativas, cargos y
/// puestos para <c>PuestoSuperiorId</c>) en paralelo vía <c>Task.WhenAll</c>,
/// valida el formulario y publica vía <see cref="IPuestosApiClient.CreateAsync"/>.
/// Tras éxito redirige al listado (PRG) preservando contexto. Sobre 409 de
/// <c>CodigoDuplicado</c> mapea el error al campo <c>Codigo</c>; sobre
/// <c>ValidationProblemDetails</c> aplica <see cref="PuestoFormHelpers.ApplyFieldErrorsToModelState"/>.
/// Fallos de transporte se traducen a un error general recuperable y conservan
/// la entrada del usuario.
/// </summary>
[Authorize]
public sealed class CreateModel(
    IPuestosApiClient puestosApiClient,
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ICargoApiClient cargoApiClient,
    ILogger<CreateModel> logger) : PageModel, IPuestoForm
{
    [BindProperty]
    public PuestoInputModel Input { get; set; } = new();

    public IReadOnlyList<UnidadOrganizativaDto> UnidadOrganizativaOptions { get; private set; } = [];

    public IReadOnlyList<CargoDto> CargoOptions { get; private set; } = [];

    public IReadOnlyList<PuestoListItemViewModel> PuestoSuperiorOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    /// <summary>
    /// Estado del banner que llega vía TempData tras un PRG exitoso.
    /// Create (sin path conflict) sólo setea el banner cuando crea; la
    /// propiedad queda pública para que la vista pueda renderizar el
    /// mensaje de feedback tras un redirect del propio Create.
    /// </summary>
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
    /// GET handler. Carga los tres catálogos en paralelo vía
    /// <c>Task.WhenAll</c>. Si cualquiera falla, se marca
    /// <see cref="ErrorMessage"/> con copy recuperable y los catálogos
    /// que sí llegaron se conservan; el form sigue visible para que el
    /// usuario pueda reintentar.
    /// </summary>
    public async Task OnGetAsync(
        string? p = null,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;

        await LoadCatalogsAsync(cancellationToken);
    }

    /// <summary>
    /// POST handler. Valida ModelState; si pasa, llama
    /// <c>POST /api/v1/puestos</c> y mapea el resultado. Sobre éxito
    /// redirige al listado (PRG) preservando filtros; sobre 409 mapea
    /// <c>CodigoDuplicado</c> al campo Codigo; sobre 400 con FieldErrors
    /// los aplica al ModelState; cualquier fallo recuperable (transporte,
    /// serialización) muestra error general y conserva input + catálogos.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new CrearPuestoRequest(
            Input.Codigo,
            Input.Nombre,
            // ModelState.IsValid ya garantizó que ambos Guid? no son null
            // gracias a [Required] en PuestoInputModel. El operador ! es
            // seguro aquí.
            Input.UnidadOrganizativaId!.Value,
            Input.CargoId!.Value,
            Input.PuestoSuperiorId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        PuestoCommandResult result;
        try
        {
            result = await puestosApiClient.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
        {
            // Transport-level failure (network down, timeout, malformed
            // body). El usuario podrá reintentar conservando su input.
            logger.LogError(ex, "Puesto create transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de puestos. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData[nameof(StatusMessage)] = $"El puesto \"{result.Value.Nombre}\" se creó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            var routeValues = BuildListRouteValues();
            return RedirectToPage("/Organizacion/Puestos/Index", routeValues);
        }

        if (result.Error is not null)
        {
            // 409 con código CodigoDuplicado → error a nivel de campo Codigo.
            if (result.Error.Type == PuestoErrorType.Conflict)
            {
                ModelState.AddModelError(PuestoFormKeys.CodigoKey, result.Error.Message);
            }
            else if (!PuestoPostResultMapper.TryMap(result, ModelState))
            {
                // No FieldErrors y no hay mensaje general (e.g., Error.Message
                // null en un failure inesperado): fallback defensivo.
                ErrorMessage = result.Error.Message;
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Construye los route values del redirect PRG hacia el listado.
    /// Mantiene <c>p</c>/<c>search</c>/<c>sort</c>/<c>status</c> sólo
    /// cuando tienen valor para no contaminar el URL.
    /// </summary>
    private object BuildListRouteValues()
    {
        var routeValues = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(ReturnPage) && int.TryParse(ReturnPage, out var page) && page > 1)
        {
            routeValues["p"] = page;
        }
        if (!string.IsNullOrWhiteSpace(ReturnSearch))
        {
            routeValues["search"] = ReturnSearch;
        }
        if (!string.IsNullOrWhiteSpace(ReturnSort))
        {
            routeValues["sort"] = ReturnSort;
        }
        if (string.Equals(ReturnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase))
        {
            routeValues["status"] = "eliminadas";
        }
        return routeValues;
    }

    /// <summary>
    /// Carga los tres catálogos en paralelo vía <c>Task.WhenAll</c>.
    /// Cualquier excepción (sincrónica o asincrónica) de uno o más
    /// catálogos se registra con <see cref="ErrorMessage"/> y el catálogo
    /// correspondiente queda vacío. El form sigue visible para permitir
    /// reintento manual. El helper <see cref="LaunchSafeAsync"/> convierte
    /// throws sincrónicos en faulted tasks (algo que el fake de tests hace:
    /// <c>GetAllAsync</c> lanza <c>HttpRequestException</c> sin envolver en
    /// <c>Task.FromException</c>) para que <c>Task.WhenAll</c> y los chequeos
    /// de estado puedan observar la falla de forma uniforme.
    /// </summary>
    private async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        // TODO: IUnidadOrganizativaApiClient no expone GetAllAsync(), por eso
        // usamos QueryAsync con pageSize=200 como workaround. Si el backend
        // implementa paginación real con pageSize menor, el dropdown de Create
        // se truncará silenciosamente. Seguimiento: exponer GetAllAsync() en el
        // interface o al menos un query con pageSize configurable.
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
