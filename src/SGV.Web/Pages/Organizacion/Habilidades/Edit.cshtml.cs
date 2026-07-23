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
/// PageModel for the Edit page of a Habilidad. Carga la habilidad por id
/// en GET y la persiste vía <see cref="IHabilidadApiClient.UpdateAsync"/> en
/// POST. El campo <c>Codigo</c> es editable y se envía al backend para
/// que la unicidad activa se evalúe contra otras Habilidades activas.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>), redirect vía
/// <see cref="IAuthSessionRedirector"/> en
/// <see cref="ErrorCategoria.Unauthorized"/>, y catch centralizado en
/// <see cref="TransportFailureClassifier.IsTransportFailure"/>. El catch
/// manual sobre tipos nativos se reemplaza por la versión opt-in
/// (incluye <c>OperationCanceledException</c> cuando el token del
/// caller NO está cancelado — preserva la cancelación cooperativa).
/// </para>
/// </summary>
[Authorize]
public sealed class EditModel(
    IHabilidadApiClient habilidadApiClient,
    ICategoriaHabilidadApiClient categoriaHabilidadApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IHabilidadForm
{
    [BindProperty]
    public HabilidadInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// Catálogo de categorías de habilidad para el &lt;select&gt; del formulario.
    /// Usa <c>null</c> como sentinel de "no cargado aún" — el helper compartido
    /// <see cref="CategoriaHabilidadCatalogLoader"/> distingue así un catálogo
    /// legítimamente vacío de uno que nunca se cargó.
    /// </summary>
    private IReadOnlyList<CategoriaHabilidadDto>? _categoriasDisponibles;
    public IReadOnlyList<CategoriaHabilidadDto> CategoriasDisponibles => _categoriasDisponibles ?? [];

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
            Input.CategoriaId = habilidad.CategoriaId;

            await LoadCategoriasAsync(cancellationToken);

            return Page();
        }
        // Issue #125: catch centralizado via TransportFailureClassifier; la
        // cancelación cooperativa del caller NO se captura (request
        // cancelado = no renderizamos). El includeOperationCanceled: true
        // acepta OperationCanceledException cuando el token del caller NO
        // fue el origen de la cancelación (preserva semántica anterior).
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(
            ex, includeOperationCanceled: !cancellationToken.IsCancellationRequested))
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
            await LoadCategoriasAsync(cancellationToken);
            return Page();
        }

        var request = new ActualizarHabilidadRequest(
            Input.Codigo,
            Input.Nombre,
            Input.CategoriaId,
            string.IsNullOrWhiteSpace(Input.Descripcion) ? null : Input.Descripcion.Trim());

        HabilidadCommandResult result;
        try
        {
            result = await habilidadApiClient.UpdateAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(
            ex, includeOperationCanceled: !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Habilidad update transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCategoriasAsync(cancellationToken);
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
            // Issue #125 / Slice 3: switch exhaustivo sobre ErrorCategoria.
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
