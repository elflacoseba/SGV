using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel for the Create page of a Cargo. Loads the catalog of
/// niveles, validates the form, and POSTs to the API using
/// <see cref="ICargoApiClient"/>. On success it PRG-redirects to the new
/// cargo's Details page with a confirmation message. On conflict (e.g. a
/// duplicate <c>Codigo</c> against an existing active cargo) the
/// field-level error is mapped back to the <c>Codigo</c> form field so
/// the user can correct it.
/// </summary>
[Authorize]
public sealed class CreateModel(
    ICargoApiClient cargoApiClient,
    ILogger<CreateModel> logger) : PageModel, ICargoForm
{
    [BindProperty]
    public CargoInputModel Input { get; set; } = new();

    public IReadOnlyList<NivelCargoDto> NivelOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => CargoFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort);

    /// <summary>
    /// Handler GET del formulario. Carga el catálogo de niveles para el dropdown.
    /// Si la carga del catálogo falla, se muestra un error recuperable.
    /// </summary>
    public async Task OnGetAsync(string? p = null, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        await LoadCatalogsAsync(cancellationToken);
    }

    /// <summary>
    /// Handler POST del formulario. Valida ModelState, llama al API
    /// <c>POST /api/v1/cargos</c> y redirige al detalle del nuevo cargo
    /// (PRG). Si el API devuelve 400 con field errors, los mapea a
    /// ModelState (prefijo <c>Input.</c>) para mostrarlos en el form.
    /// Si devuelve 409, se traduce a un error a nivel del campo
    /// <c>Codigo</c>. Si la respuesta falla por otro motivo, se muestra un
    /// error general y se vuelve a cargar el catálogo.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new CrearCargoRequest(
            Input.Codigo,
            Input.Nombre,
            Input.NivelId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        var result = await cargoApiClient.CreateAsync(request, cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"El cargo \"{result.Value.Nombre}\" se creó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/Cargos/Details", new { id = result.Value.Id });
        }

        if (result.Error is not null)
        {
            // Conflict 409 (código duplicado) → error a nivel de campo Codigo.
            if (result.Error.Type == CargoErrorType.Conflict)
            {
                ModelState.AddModelError("Input.Codigo", result.Error.Message);
            }
            else if (result.FieldErrors is { Count: > 0 })
            {
                CargoFormHelpers.ApplyFieldErrorsToModelState(ModelState, result.FieldErrors);
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
            NivelOptions = await cargoApiClient.GetNivelesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load niveles-cargo catalog.");
            ErrorMessage = "No se pudo cargar el catálogo de niveles. Intentá nuevamente.";
        }
    }
}
