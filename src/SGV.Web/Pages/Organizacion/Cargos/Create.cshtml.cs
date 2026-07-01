using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
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
    /// GET handler for the form. Loads the nivel catalog for the dropdown.
    /// If the catalog load fails, a recoverable error is shown.
    /// </summary>
    public async Task OnGetAsync(string? p = null, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        await LoadCatalogsAsync(cancellationToken);
    }

    /// <summary>
    /// POST handler for the form. Validates ModelState, calls the API at
    /// <c>POST /api/v1/cargos</c>, and redirects to the new cargo's
    /// Details page (PRG). If the API returns 400 with field errors, they
    /// are mapped into ModelState (prefixed with <c>Input.</c>) so the
    /// form renders them next to the right field. A 409 is translated
    /// into a field-level error on <c>Codigo</c>. Any other failure shows
    /// a general error and reloads the catalog.
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

        CargoCommandResult result;
        try
        {
            result = await cargoApiClient.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
        {
            // Transport-level failure (network down, timeout, malformed body).
            // Map to a recoverable error: keep user input, reload the catalog,
            // re-render the page so the user can retry. We do not propagate
            // as 500 because the user action is recoverable.
            logger.LogError(ex, "Cargo create transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de cargos. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"El cargo \"{result.Value.Nombre}\" se creó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/Cargos/Details", new { id = result.Value.Id });
        }

        if (result.Error is not null)
        {
            // Conflict 409 (duplicate Codigo) → field-level error on Codigo.
            if (result.Error.Type == CargoErrorType.Conflict)
            {
                ModelState.AddModelError(CargoFormKeys.CodigoKey, result.Error.Message);
            }
            else if (!CargoPostResultMapper.TryMap(result, ModelState))
            {
                // No FieldErrors and no general error message; defensive fallback
                // for unexpected shapes (e.g., null Error.Message on a non-Conflict).
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
