using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

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
/// </summary>
[Authorize]
public sealed class EditModel(
    ICargoApiClient cargoApiClient,
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
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => CargoFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort);

    /// <summary>
    /// GET handler. Carga el cargo por id y el catálogo de niveles. Si el
    /// cargo no existe o la consulta falla, marca <see cref="IsRecoverable"/>
    /// y muestra un mensaje recuperable sin renderizar el formulario. Los
    /// parámetros <c>p</c>, <c>search</c> y <c>sort</c> se preservan para
    /// los enlaces de retorno al listado.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] string? page = null,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

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

            await LoadCatalogsAsync(cancellationToken);
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
        [FromQuery(Name = "p")] string? page = null,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new ActualizarCargoRequest(
            Input.Codigo,
            Input.Nombre,
            Input.NivelId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        CargoCommandResult result;
        try
        {
            result = await cargoApiClient.UpdateAsync(id, request, cancellationToken);
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
            logger.LogError(ex, "Cargo update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de cargos. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"El cargo \"{result.Value.Nombre}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/Cargos/Edit", new { id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort });
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
            logger.LogError(ex, "Failed to load niveles-cargo catalog for edit page.");
            ErrorMessage = "No se pudo cargar el catálogo de niveles. Intentá nuevamente.";
        }
    }
}