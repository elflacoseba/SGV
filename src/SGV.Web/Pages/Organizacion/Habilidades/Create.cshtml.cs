using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Categorias.Consultas;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel for the Create page of a Habilidad. POSTs the new habilidad
/// via <see cref="IHabilidadApiClient"/>. On success PRG-redirects to the
/// new habilidad's Details page with a confirmation TempData. On conflict
/// (duplicate <c>Codigo</c>) the field-level error is mapped back to the
/// <c>Codigo</c> form field so the user can correct it.
/// <para>
/// Issue #125 / Slice 3: el switch sobre <see cref="HabilidadErrorType"/>
/// se reemplaza por un switch exhaustivo sobre
/// <see cref="ErrorCategoria"/>. <c>Unauthorized</c> delega el redirect
/// a <see cref="IAuthSessionRedirector"/>. El catch manual sobre tipos
/// de excepción nativos se reemplaza por
/// <see cref="TransportFailureClassifier.IsTransportFailure"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class CreateModel(
    IHabilidadApiClient habilidadApiClient,
    ICategoriaHabilidadApiClient categoriaHabilidadApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IHabilidadForm
{
    [BindProperty]
    public HabilidadInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    /// <summary>
    /// Catálogo de categorías de habilidad para el &lt;select&gt; del formulario.
    /// Usa <c>null</c> como sentinel de "no cargado aún" — el helper compartido
    /// <see cref="CategoriaHabilidadCatalogLoader"/> distingue así un catálogo
    /// legítimamente vacío de uno que nunca se cargó.
    /// </summary>
    private IReadOnlyList<CategoriaHabilidadDto>? _categoriasDisponibles;
    public IReadOnlyList<CategoriaHabilidadDto> CategoriasDisponibles => _categoriasDisponibles ?? [];

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

        await LoadCategoriasAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriasAsync(cancellationToken);
            return Page();
        }

        var request = new CrearHabilidadRequest(
            Input.Codigo,
            Input.Nombre,
            Input.CategoriaId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        HabilidadCommandResult result;
        try
        {
            result = await habilidadApiClient.CreateAsync(request, cancellationToken);
        }
        // Issue #125 / REQ-3 (web-apiclient-transport-contract): las excepciones
        // nativas se propagan; este catch absorbe solo los fallos de transporte
        // recuperables vía TransportFailureClassifier.
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Habilidad create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCategoriasAsync(cancellationToken);
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
            // Issue #125 / Slice 3: el switch sobre *ErrorType se reemplaza
            // por uno sobre la nueva taxonomía ErrorCategoria. Los 7 miembros
            // están cubiertos sin default: explícito para forzar revisión si
            // se agrega una variante (CS8524 aceptable, ver design §8.1).
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                ErrorMessage = PageFeedback.UnauthorizedMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
            else if (result.Error.Categoria == ErrorCategoria.Conflict)
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
                ErrorMessage = ErrorCategoryMapper.Map(result.Error.Categoria,
                    notFoundMessage: "El recurso solicitado no está disponible.",
                    conflictMessage: "Conflicto al persistir la habilidad.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCategoriasAsync(cancellationToken);
        return Page();
    }

    private async Task LoadCategoriasAsync(CancellationToken ct)
    {
        var (categorias, transportFailed) = await CategoriaHabilidadCatalogLoader.LoadAsync(
            categoriaHabilidadApiClient, logger, _categoriasDisponibles, ct);
        _categoriasDisponibles = categorias;
        if (transportFailed && string.IsNullOrWhiteSpace(ErrorMessage))
        {
            ErrorMessage = "No se pudo cargar el catálogo de categorías.";
        }
    }
}
