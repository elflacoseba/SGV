using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Web.Integration.Habilidades;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel for the Create page of a Habilidad. POSTs the new habilidad
/// via <see cref="IHabilidadApiClient"/>. On success PRG-redirects to the
/// new habilidad's Details page with a confirmation TempData. On conflict
/// (duplicate <c>Codigo</c>) the field-level error is mapped back to the
/// <c>Codigo</c> form field so the user can correct it.
/// </summary>
[Authorize]
public sealed class CreateModel(
    IHabilidadApiClient habilidadApiClient,
    ILogger<CreateModel> logger) : PageModel, IHabilidadForm
{
    [BindProperty]
    public HabilidadInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

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

    public async Task OnGetAsync([FromQuery(Name = "p")] int p = 1, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = Math.Max(1, p);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CrearHabilidadRequest(
            Input.Codigo,
            Input.Nombre,
            string.IsNullOrWhiteSpace(Input.Categoria) ? null : Input.Categoria.Trim(),
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        HabilidadCommandResult result;
        try
        {
            result = await habilidadApiClient.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is OperationCanceledException)
        {
            logger.LogError(ex, "Habilidad create transport failure.");
            ErrorMessage = "No se pudo contactar al servicio de habilidades. Intentá nuevamente.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La habilidad \"{result.Value.Nombre}\" se creó correctamente.";
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