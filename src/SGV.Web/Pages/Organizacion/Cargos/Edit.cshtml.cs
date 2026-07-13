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

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel for the Edit page of a Cargo. Loads the cargo and the
/// nivel catalog in GET, prepopulates <see cref="ICargoForm.Input"/>,
/// and POSTs the editable fields (including <c>Codigo</c>) via
/// <see cref="ICargoApiClient.UpdateAsync"/>. On success it
/// PRG-redirects to itself with a confirmation TempData. On 409 the
/// duplicate <c>Codigo</c> is mapped to a field-level error on
/// <c>Codigo</c>; on 400 the backend <c>ValidationProblemDetails</c> are
/// translated via <see cref="CargoPostResultMapper.TryMap"/>.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>). <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class EditModel(
    ICargoApiClient cargoApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, ICargoForm
{
    [BindProperty]
    public CargoInputModel Input { get; set; } = new();

    public IReadOnlyList<NivelCargoDto> NivelOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// Indica si el cargo solicitado no pudo cargarse (404 o error de
    /// transporte). En ese estado la vista muestra un mensaje
    /// recuperable y oculta el formulario.
    /// </summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>
    /// Mensaje de estado (success/warning) que llega vía TempData tras un PRG.
    /// </summary>
    public string? StatusMessage => TempData["StatusMessage"] as string;

    public string StatusKind => TempData["StatusKind"] as string ?? "success";

    [BindProperty]
    public int ReturnPage { get; set; } = 1;

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => CargoFormHelpers.BuildReturnToListUrl(
        Url,
        ReturnPage.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ReturnSearch,
        ReturnSort);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler. Carga el cargo por id y el catálogo de niveles. Si el
    /// cargo no existe o la consulta falla, marca <see cref="IsRecoverable"/>
    /// y muestra un mensaje recuperable sin renderizar el formulario. Los
    /// parámetros <c>p</c>, <c>search</c> y <c>sort</c> se preservan para
    /// los enlaces de retorno al listado.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
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

        ReturnPage = Math.Max(1, page);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

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

            Input.Codigo = cargo.Codigo;
            Input.Nombre = cargo.Nombre;
            Input.Descripcion = cargo.Descripcion;
            Input.NivelId = cargo.NivelId;

            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load edit page for cargo {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el cargo. Intentá nuevamente.";
            return Page();
        }
    }

    /// <summary>
    /// POST handler. Valida ModelState, llama <c>PUT /api/v1/cargos/{id}</c>,
    /// y mapea el resultado a feedback del usuario. Tras éxito, PRG a sí
    /// mismo con TempData. Tras fallo de validación/conflicto, recarga el
    /// catálogo y re-renderiza el formulario con los mensajes de error.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        Guid id,
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = Math.Max(1, page);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        var request = new ActualizarCargoRequest(
            Input.Codigo,
            Input.Nombre,
            // ModelState.IsValid ya garantizó que NivelId no es null (ver
            // [Required] en CargoInputModel). El operador ! es seguro aquí.
            Input.NivelId!.Value,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        CargoCommandResult result;
        try
        {
            result = await cargoApiClient.UpdateAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed body).
            // Map to a recoverable error: keep user input, reload the catalog,
            // re-render the page so the user can retry.
            logger.LogError(ex, "Cargo update transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"El cargo \"{result.Value.Nombre}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/Cargos/Details", new { id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort });
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
                await LoadCatalogsAsync(id, cancellationToken);
                return Page();
            }

            // Conflict 409 (duplicate Codigo) → field-level error on Codigo.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                ModelState.AddModelError(CargoFormKeys.CodigoKey, result.Error.Message);
            }
            else if (!CargoPostResultMapper.TryMap(result, ModelState))
            {
                // No FieldErrors and no general error message; defensivo para
                // ErrorCategoria.Validation/Transport/Unexpected/Forbidden.
                ErrorMessage = MapCategoriaToMessage(result.Error.Categoria);
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(id, cancellationToken);
        return Page();
    }

    /// <summary>
    /// Switch exhaustivo sobre <see cref="ErrorCategoria"/>. Verbatim del
    /// patrón de <see cref="CreateModel.MapCategoriaToMessage"/>; espejado
    /// para que cada PageModel pueda invocarlo sin pasar por el helper
    /// de aplicación.
    /// </summary>
    internal static string MapCategoriaToMessage(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => "El cargo solicitado no está disponible.",
        ErrorCategoria.Conflict => "Conflicto al persistir el cargo.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            "Unauthorized se redirige vía IAuthSessionRedirector antes de mostrar mensaje inline."),
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

    private async Task LoadCatalogsAsync(Guid cargoId, CancellationToken cancellationToken)
    {
        try
        {
            NivelOptions = await cargoApiClient.GetNivelesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load niveles-cargo catalog for edit page (cargoId={CargoId}).", cargoId);
            ErrorMessage = "No se pudo cargar el catálogo de niveles. Intentá nuevamente.";
        }
    }
}
