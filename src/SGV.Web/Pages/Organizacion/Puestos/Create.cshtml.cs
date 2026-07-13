using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

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
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/>. <c>Unauthorized</c> redirige vía
/// <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class CreateModel(
    IPuestosApiClient puestosApiClient,
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ICargoApiClient cargoApiClient,
    IAuthSessionRedirector authRedirector,
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
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

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

    /// <summary>
    /// GET handler. Carga los tres catálogos en paralelo vía
    /// <c>Task.WhenAll</c>. Si cualquiera falla, se marca
    /// <see cref="ErrorMessage"/> con copy recuperable y los catálogos
    /// que sí llegaron se conservan; el form sigue visible para que el
    /// usuario pueda reintentar.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        string? p = null,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            // Patrón canónico del repo (ver Habilidades.cshtml.cs): Forbid()
            // delega al cookie scheme, que redirige a AccessDeniedPath
            // ("/error/403" configurado en Program.cs). Es testeable y
            // simétrico con los POST handlers del módulo.
            return Forbid();
        }

        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(status) ?? string.Empty;

        await LoadCatalogsAsync(cancellationToken);
        return Page();
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
        if (!EsAdministrador)
        {
            return Forbid();
        }

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
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed
            // body). El usuario podrá reintentar conservando su input.
            logger.LogError(ex, "Puesto create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            PageFeedback.SetSuccess(TempData, $"El puesto \"{result.Value.Nombre}\" se creó correctamente.");

            var routeValues = RouteValuesPreserver.BuildListRouteValues(
                ParseReturnPage(),
                ReturnSearch,
                ReturnSort,
                ReturnStatus);
            return RedirectToPage("/Organizacion/Puestos/Index", routeValues);
        }

        if (result.Error is not null)
        {
            // Issue #125 / Slice 3: switch exhaustivo sobre ErrorCategoria.
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                ErrorMessage = PageFeedback.UnauthorizedMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            // 409 con código CodigoDuplicado → error a nivel de campo Codigo.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                ModelState.AddModelError(PuestoFormKeys.CodigoKey, result.Error.Message);
            }
            else if (!PuestoPostResultMapper.TryMap(result, ModelState))
            {
                // No FieldErrors y no hay mensaje general: fallback defensivo.
                ErrorMessage = MapCategoriaToMessage(result.Error.Categoria);
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Switch exhaustivo sobre <see cref="ErrorCategoria"/>. Cubre las 7
    /// variantes sin <c>default</c> silencioso (design §8.1, F3).
    /// <c>Unauthorized</c> lanza porque su flujo es redirigir vía
    /// <see cref="IAuthSessionRedirector"/> antes de mostrar mensaje inline.
    /// </summary>
    internal static string MapCategoriaToMessage(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => "El puesto solicitado no está disponible.",
        ErrorCategoria.Conflict => "Conflicto al persistir el puesto.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            "Unauthorized se redirige vía IAuthSessionRedirector antes de mostrar mensaje inline."),
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

    private int ParseReturnPage() =>
        int.TryParse(ReturnPage, out var page) ? Math.Max(1, page) : 1;

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

        var unidadesTask = PuestoFormHelpers.LaunchSafeAsync(() => unidadOrganizativaApiClient.GetAllActivasAsync(cancellationToken: cancellationToken));
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
            UnidadOrganizativaOptions = unidadesTask.Result;
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
