using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Web.Integration.Habilidades;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel for the Edit page of a Habilidad. Carga la habilidad por id
/// en GET y la persiste vía <see cref="IHabilidadApiClient.UpdateAsync"/> en
/// POST. El campo <c>Codigo</c> es editable y se envía al backend para
/// que la unicidad activa se evalúe contra otras Habilidades activas.
/// </summary>
[Authorize]
public sealed class EditModel(
    IHabilidadApiClient habilidadApiClient,
    ILogger<EditModel> logger) : PageModel, IHabilidadForm
{
    [BindProperty]
    public HabilidadInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// <c>true</c> cuando la habilidad solicitada no existe o la consulta
    /// falla; la vista muestra un estado recuperable sin renderizar el form.
    /// </summary>
    public bool IsRecoverable { get; private set; }

    public string? StatusMessage => TempData["StatusMessage"] as string;

    public string StatusKind => TempData["StatusKind"] as string ?? "success";

    [BindProperty]
    public int ReturnPage { get; set; } = 1;

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => HabilidadFormHelpers.BuildReturnToListUrl(
        Url,
        ReturnPage,
        ReturnSearch,
        ReturnSort);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        ReturnPage = Math.Max(1, page);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        try
        {
            var habilidad = await habilidadApiClient.GetByIdAsync(id, cancellationToken);
            if (habilidad is null)
            {
                IsRecoverable = true;
                ErrorMessage = "La habilidad solicitada no está disponible.";
                logger.LogWarning("Habilidad with Id {HabilidadId} was not found or is no longer available.", id);
                return Page();
            }

            Input.Codigo = habilidad.Codigo;
            Input.Nombre = habilidad.Nombre;
            Input.Descripcion = habilidad.Descripcion;
            Input.Categoria = habilidad.Categoria;

            return Page();
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            logger.LogError(ex, "Habilidad edit GET transport failure.");
            IsRecoverable = true;
            ErrorMessage = "La habilidad solicitada no está disponible.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new ActualizarHabilidadRequest(
            Input.Codigo,
            Input.Nombre,
            string.IsNullOrWhiteSpace(Input.Categoria) ? null : Input.Categoria.Trim(),
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        HabilidadCommandResult result;
        try
        {
            result = await habilidadApiClient.UpdateAsync(id, request, cancellationToken);
        }
        // Cancelación cooperativa: si el cliente cerró el navegador / navegó
        // a otra página, el HttpContext.RequestAborted se cancela y el
        // cliente API propaga OperationCanceledException. NO la capturamos:
        // intentar renderizar una página en un request cancelado desperdicia
        // trabajo y puede generar logs ruidosos. Dejamos que la excepción
        // suba para que el pipeline la traduzca a ClientDisconnectedException.
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is JsonException ||
            ((ex is TaskCanceledException || ex is OperationCanceledException)
                && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Habilidad update transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La habilidad \"{result.Value.Nombre}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Organizacion/Habilidades/Details", new { id = result.Value.Id });
        }

        if (result.Error is not null)
        {
            if (result.Error.Type == HabilidadErrorType.Conflict)
            {
                ModelState.AddModelError("Input.Codigo", result.Error.Message);
            }
            else if (result.FieldErrors is { Count: > 0 })
            {
                foreach (var kvp in result.FieldErrors)
                {
                    var key = kvp.Key.StartsWith("Input.", StringComparison.OrdinalIgnoreCase)
                        ? kvp.Key
                        : "Input." + kvp.Key;
                    ModelState.AddModelError(key, string.Join(" ", kvp.Value));
                }
            }
            else
            {
                ErrorMessage = result.Error.Message;
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
        }

        return Page();
    }
}