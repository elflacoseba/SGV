using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Authorize]
public sealed class EditModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ILogger<EditModel> logger) : PageModel, IUnidadOrganizativaForm
{
    [BindProperty]
    public UnidadOrganizativaInputModel Input { get; set; } = new();

    [BindProperty]
    public string? OriginalUnidadPadreId { get; set; }

    public IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; private set; } = [];

    public IReadOnlyList<ParentOptionViewModel> ParentOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

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

    public string ReturnToListUrl => UnidadOrganizativaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort, ReturnView);

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
        CancellationToken cancellationToken = default)
    {
        ReturnPage = returnPage ?? p ?? page;
        ReturnSearch = returnSearch ?? search;
        ReturnSort = returnSort ?? sort;
        ReturnView = returnView ?? view;

        try
        {
            var unidad = await unidadOrganizativaApiClient.GetByIdAsync(id, cancellationToken);
            if (unidad is null)
            {
                TempData["StatusMessage"] = "La unidad organizativa solicitada no existe o ya no está disponible.";
                TempData["StatusKind"] = "warning";
                return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { p = ReturnPage, search = ReturnSearch, sort = ReturnSort });
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

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        var request = new ActualizarUnidadOrganizativaRequest(
            Input.Codigo,
            Input.Nombre,
            Input.TipoUnidadOrganizativaId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim(),
            Input.VigenteDesde,
            Input.VigenteHasta);

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
                    return RedirectToPage("/Organizacion/UnidadesOrganizativas/Edit", new { id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort, returnView = ReturnView });
                }
            }

            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView });
        }

        if (result.Error is not null)
        {
            if (result.FieldErrors is { Count: > 0 })
            {
                UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState(ModelState, result.FieldErrors);
            }
            else
            {
                ErrorMessage = result.Error.Message;
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
        }

        await LoadCatalogsAsync(id, cancellationToken);
        return Page();
    }

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
