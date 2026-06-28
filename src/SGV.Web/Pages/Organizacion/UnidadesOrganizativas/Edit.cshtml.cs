using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; private set; } = [];

    public IReadOnlyList<ParentOptionViewModel> ParentOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public string ReturnPage { get; private set; } = string.Empty;

    public string ReturnSearch { get; private set; } = string.Empty;

    public string ReturnSort { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, string? page = null, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        try
        {
            var unidad = await unidadOrganizativaApiClient.GetByIdAsync(id, cancellationToken);
            if (unidad is null)
            {
                TempData["StatusMessage"] = "La unidad organizativa solicitada no existe o ya no está disponible.";
                TempData["StatusKind"] = "warning";
                return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { page = ReturnPage, search = ReturnSearch, sort = ReturnSort });
            }

            await LoadCatalogsAsync(id, cancellationToken);

            Input.Codigo = unidad.Codigo;
            Input.Nombre = unidad.Nombre;
            Input.Descripcion = unidad.Descripcion;
            Input.TipoUnidadOrganizativaId = unidad.TipoUnidadOrganizativaId;
            Input.UnidadPadreId = unidad.UnidadPadreId;
            Input.VigenteDesde = unidad.VigenteDesde;
            Input.VigenteHasta = unidad.VigenteHasta;

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
        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(id, cancellationToken);
            return Page();
        }

        var request = new SGV.Aplicacion.Organizacion.Comandos.ActualizarUnidadOrganizativaRequest(
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
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id });
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
}
