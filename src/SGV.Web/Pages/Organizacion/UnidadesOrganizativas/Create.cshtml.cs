using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// PageModel para la página Create de unidades organizativas.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/>. <c>Unauthorized</c> redirige vía
/// <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class CreateModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IUnidadOrganizativaForm
{
    [BindProperty]
    public UnidadOrganizativaInputModel Input { get; set; } = new();

    public IReadOnlyList<TipoUnidadOrganizativaDto> TipoOptions { get; private set; } = [];

    public IReadOnlyList<ParentOptionViewModel> ParentOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public string? ReturnView { get; set; }

    [BindProperty]
    public string? ReturnStatus { get; set; }

    public string ReturnToListUrl => UnidadOrganizativaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort, ReturnView, ReturnStatus);

    public async Task OnGetAsync(string? p = null, string? page = null, string? search = null, string? sort = null, string? view = null, string? returnView = null, string? returnStatus = null, CancellationToken cancellationToken = default)
    {
        ReturnPage = p ?? page ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;
        ReturnView = returnView ?? view ?? string.Empty;
        ReturnStatus = returnStatus ?? string.Empty;

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
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Details", new { id = result.Value.Id, returnPage = ReturnPage, returnSearch = ReturnSearch, returnSort = ReturnSort, returnView = ReturnView, returnStatus = ReturnStatus });
        }

        // Validation or conflict
        if (result.Error is not null)
        {
            // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                ErrorMessage = PageFeedback.UnauthorizedMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            if (result.FieldErrors is { Count: > 0 })
            {
                UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState(ModelState, result.FieldErrors);
            }
            else
            {
                ErrorMessage = MapCategoriaToMessage(result.Error.Categoria);
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Switch exhaustivo sobre <see cref="ErrorCategoria"/>. Cubre las 7
    /// variantes sin <c>default</c> silencioso (design §8.1, F3).
    /// <c>Unauthorized</c> lanza porque su flujo es redirigir vía
    /// <see cref="IAuthSessionRedirector"/> antes de mostrar mensaje inline.
    /// </summary>
    internal static string MapCategoriaToMessage(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => "La unidad organizativa solicitada no está disponible.",
        ErrorCategoria.Conflict => "Conflicto al persistir la unidad organizativa.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            "Unauthorized se redirige vía IAuthSessionRedirector antes de mostrar mensaje inline."),
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

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
