using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Authorize]
public sealed class CreateModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    ILogger<CreateModel> logger) : PageModel, IUnidadOrganizativaForm
{
    [BindProperty]
    public UnidadOrganizativaInputModel Input { get; set; } = new();

    public IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; private set; } = [];

    public IReadOnlyList<ParentOptionViewModel> ParentOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public string ReturnPage { get; private set; } = string.Empty;

    public string ReturnSearch { get; private set; } = string.Empty;

    public string ReturnSort { get; private set; } = string.Empty;

    public async Task OnGetAsync(string? page = null, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        await LoadCatalogsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new CrearUnidadOrganizativaRequest(
            Input.Codigo,
            Input.Nombre,
            Input.TipoUnidadOrganizativaId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim(),
            Input.VigenteDesde,
            Input.VigenteHasta,
            Input.UnidadPadreId);

        var result = await unidadOrganizativaApiClient.CreateAsync(request, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La unidad organizativa \"{result.Value.Nombre}\" se creó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id = result.Value.Id });
        }

        // Validation or conflict
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

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    private async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tiposTask = unidadOrganizativaApiClient.GetTiposAsync(cancellationToken);
            var treeTask = unidadOrganizativaApiClient.GetTreeAsync(cancellationToken);

            await Task.WhenAll(tiposTask, treeTask);

            TipoOptions = tiposTask.Result;
            ParentOptions = UnidadOrganizativaFormHelpers.FlattenTree(treeTask.Result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load create-page catalogs.");
            ErrorMessage = "No se pudieron cargar los catálogos necesarios. Intentá nuevamente.";
        }
    }
}
