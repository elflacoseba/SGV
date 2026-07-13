using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// PageModel para la página Edit de unidades organizativas.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/>. <c>Unauthorized</c> redirige vía
/// <see cref="IAuthSessionRedirector"/>. El filtro manual de
/// <c>OperationCanceledException</c> (exploration §3) se reemplaza por
/// <see cref="TransportFailureClassifier.IsTransportFailure"/> con
/// <c>includeOperationCanceled: true</c> explícito (opt-in).
/// </para>
/// </summary>
[Authorize]
public sealed class EditModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IUnidadOrganizativaForm
{
    public Guid CurrentId { get; private set; }

    public bool IsRecoverable { get; private set; }
    [BindProperty]
    public UnidadOrganizativaInputModel Input { get; set; } = new();

    [BindProperty]
    public string? OriginalUnidadPadreId { get; set; }

    public IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; private set; } = [];

    public IReadOnlyList<ParentOptionViewModel> ParentOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public string? ReturnView { get; set; }

    [BindProperty]
    public string? ReturnStatus { get; set; }

    public string ReturnToListUrl => UnidadOrganizativaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort, ReturnView, ReturnStatus);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? p = null,
        string? page = null,
        string? search = null,
        string? sort = null,
        string? view = null,
        string? returnPage = null,
        string? returnSearch = null,
        string? returnSort = null,
        string? returnView = null,
        string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = returnPage ?? p ?? page;
        ReturnSearch = returnSearch ?? search;
        ReturnSort = returnSort ?? sort;
        ReturnView = returnView ?? view;
        ReturnStatus = returnStatus;

        CurrentId = id;

        try
        {
            var unidad = await unidadOrganizativaApiClient.GetByIdAsync(id, cancellationToken);
            if (unidad is null)
            {
                IsRecoverable = true;
                return Page();
            }

            await LoadCatalogsAsync(id, cancellationToken);

            Input.Codigo = unidad.Codigo;
            Input.Nombre = unidad.Nombre;
            Input.Descripcion = unidad.Descripcion;
            Input.TipoUnidadOrganizativaId = unidad.TipoUnidadOrganizativaId;
            Input.UnidadPadreId = unidad.UnidadPadreId;
            Input.VigenteDesde = unidad.VigenteDesde;
            Input.VigenteHasta = unidad.VigenteHasta;
            OriginalUnidadPadreId = unidad.UnidadPadreId?.ToString();

            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load edit page for unidad organizativa {Id}.", id);
            ErrorMessage = "No se pudo cargar la unidad organizativa. Intentá nuevamente.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReturnPage = string.IsNullOrWhiteSpace(ReturnPage) ? NormalizePostedValue(Request.Form[nameof(ReturnPage)]) : ReturnPage;
        ReturnSearch = string.IsNullOrWhiteSpace(ReturnSearch) ? NormalizePostedValue(Request.Form[nameof(ReturnSearch)]) : ReturnSearch;
        ReturnSort = string.IsNullOrWhiteSpace(ReturnSort) ? NormalizePostedValue(Request.Form[nameof(ReturnSort)]) : ReturnSort;
        ReturnView = string.IsNullOrWhiteSpace(ReturnView) ? NormalizePostedValue(Request.Form[nameof(ReturnView)]) : ReturnView;
        ReturnStatus = string.IsNullOrWhiteSpace(ReturnStatus) ? NormalizePostedValue(Request.Form[nameof(ReturnStatus)]) : ReturnStatus;

        // PR3: Codigo es inmutable en Edit. El input NO se renderiza en
        // _Form.cshtml (gateado por IsEdit), pero el modelo de input todavía
        // declara `[Required]` para Create. Pre-populamos desde el DTO y
        // removemos el error de ModelState que el binder pudo haber agregado
        // si la versión del browser trae un cache stale o si un cliente
        // malicioso intenta inyectar el campo (paridad con Puestos/Edit).
        try
        {
            var current = await unidadOrganizativaApiClient.GetByIdAsync(id, cancellationToken);
            if (current is null)
            {
                IsRecoverable = true;
                ErrorMessage = "La unidad organizativa solicitada no está disponible.";
                logger.LogWarning("Unidad organizativa {Id} was not found during POST.", id);
                return Page();
            }
            Input.Codigo = current.Codigo;
            ModelState.Remove("Input.Codigo");
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(
            ex, includeOperationCanceled: true))
        {
            logger.LogError(ex, "Failed to load unidad {Id} during POST prepopulate.", id);
            ErrorMessage = "No se pudo cargar la unidad organizativa. Intentá nuevamente.";
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        var request = new ActualizarUnidadOrganizativaRequest(
            Input.Nombre,
            Input.TipoUnidadOrganizativaId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim(),
            Input.VigenteDesde,
            Input.VigenteHasta,
            Input.UnidadPadreId);

        var result = await unidadOrganizativaApiClient.UpdateAsync(id, request, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La unidad organizativa \"{result.Value.Nombre}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";

            // Detect parent change by comparing original snapshot with submitted value
            Guid? originalParentId = null;
            if (Guid.TryParse(OriginalUnidadPadreId, out var parsed))
                originalParentId = parsed;

            if (originalParentId != Input.UnidadPadreId)
            {
                var changeResult = await unidadOrganizativaApiClient.ChangeParentAsync(
                    id,
                    new CambiarUnidadPadreRequest(Input.UnidadPadreId),
                    cancellationToken);

                if (!changeResult.IsSuccess)
                {
                    // Partial success: data saved but parent change failed
                    TempData["StatusMessage"] = "Se guardaron los datos generales, pero no se pudo actualizar la unidad padre.";
                    TempData["StatusKind"] = "warning";
                    return RedirectToPage("/Organizacion/UnidadesOrganizativas/Edit", new { id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort, returnView = ReturnView, returnStatus = ReturnStatus });
                }
            }

            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView, returnStatus = ReturnStatus });
        }

        if (result.Error is not null)
        {
            // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
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

            if (result.FieldErrors is { Count: > 0 })
            {
                UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState(ModelState, result.FieldErrors);
            }
            else
            {
                ErrorMessage = MapCategoriaToMessage(result.Error.Categoria);
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReturnPage = NormalizePostedValue(Request.Form[nameof(ReturnPage)].FirstOrDefault());
        ReturnSearch = NormalizePostedValue(Request.Form[nameof(ReturnSearch)].FirstOrDefault());
        ReturnSort = NormalizePostedValue(Request.Form[nameof(ReturnSort)].FirstOrDefault());
        ReturnView = NormalizePostedValue(Request.Form[nameof(ReturnView)].FirstOrDefault());
        ReturnStatus = NormalizePostedValue(Request.Form[nameof(ReturnStatus)].FirstOrDefault());

        var result = await unidadOrganizativaApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData["StatusMessage"] = "La unidad organizativa se reactivó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView, returnStatus = ReturnStatus });
        }

        // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var categoria = result.Error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Conflict => $"No se pudo reactivar la unidad organizativa. {result.Error.Message}",
            ErrorCategoria.NotFound => "La unidad organizativa ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            _ => MapCategoriaToMessage(categoria)
        };

        TempData["StatusMessage"] = message;
        TempData["StatusKind"] = "danger";
        IsRecoverable = true;
        CurrentId = id;
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
        ErrorCategoria.NotFound => "La unidad organizativa solicitada no está disponible.",
        ErrorCategoria.Conflict => "Conflicto al persistir la unidad organizativa.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => PageFeedback.UnauthorizedMessage,
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

    private async Task LoadCatalogsAsync(Guid currentId, CancellationToken cancellationToken)
    {
        try
        {
            var tiposTask = unidadOrganizativaApiClient.GetTiposAsync(cancellationToken);
            var treeTask = unidadOrganizativaApiClient.GetTreeAsync(cancellationToken);

            await Task.WhenAll(tiposTask, treeTask);

            TipoOptions = tiposTask.Result;
            ParentOptions = UnidadOrganizativaFormHelpers.FlattenTree(treeTask.Result, excludeSubtreeRootId: currentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load edit-page catalogs.");
            ErrorMessage = "No se pudieron cargar los catálogos necesarios. Intentá nuevamente.";
        }
    }

    private static string? NormalizePostedValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
