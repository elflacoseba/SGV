using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel de Edit del módulo Puestos. Maneja GET (carga + catálogos) y
/// POST (pre-populate → validar → ejecutar → PRG / error). La lógica POST
/// pesada delega a <see cref="PuestoEditPostHandler"/>.
/// </summary>
[Authorize]
public sealed class EditModel(
    IPuestosApiClient puestosApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IPuestoForm
{
    // ──────────────────────────────────────────────
    // Exposed for PuestoEditPostHandler
    // ──────────────────────────────────────────────

    internal IPuestosApiClient PuestosApiClient => puestosApiClient;
    internal IAuthSessionRedirector AuthRedirector => authRedirector;
    internal ILogger<EditModel> Logger => logger;

    // ──────────────────────────────────────────────
    // Properties
    // ──────────────────────────────────────────────

    [BindProperty]
    public PuestoInputModel Input { get; set; } = new();

    public IReadOnlyList<UnidadOrganizativaDto> UnidadOrganizativaOptions { get; private set; } = [];

    public IReadOnlyList<CargoDto> CargoOptions { get; private set; } = [];

    public IReadOnlyList<PuestoListItemViewModel> PuestoSuperiorOptions { get; private set; } = [];

    public string? ErrorMessage { get; internal set; }

    public bool IsEdit => true;

    public bool IsRecoverable { get; internal set; }

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public string? ReturnStatus { get; set; }

    public string ReturnToListUrl => PuestoFormHelpers.BuildReturnToListUrl(
        Url, ReturnPage, ReturnSearch, ReturnSort, ReturnStatus);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    // ──────────────────────────────────────────────
    // GET
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
            return Forbid();

        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(returnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;

        try
        {
            var puesto = await puestosApiClient.GetByIdAsync(id, cancellationToken);
            if (puesto is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El puesto solicitado no está disponible.";
                logger.LogWarning("Puesto with Id {PuestoId} was not found or is no longer available.", id);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            Input.Nombre = puesto.Nombre;
            Input.Descripcion = puesto.Descripcion;
            Input.PuestoSuperiorId = puesto.PuestoSuperiorId;

            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load edit page for puesto {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el puesto. Intentá nuevamente.";
            return Page();
        }
    }

    // ──────────────────────────────────────────────
    // POST — delega al handler extraído
    // ──────────────────────────────────────────────

    public Task<IActionResult> OnPostAsync(
        Guid id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
            return Task.FromResult<IActionResult>(Forbid());

        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search;
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? string.Empty : sort;
        ReturnStatus = string.Equals(returnStatus, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : string.Empty;

        return PuestoEditPostHandler.HandleAsync(this, id, cancellationToken);
    }

    // ──────────────────────────────────────────────
    // Internal — reused by PuestoEditPostHandler
    // ──────────────────────────────────────────────

    internal async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        var puestosTask = PuestoFormHelpers.LaunchSafeAsync(
            () => puestosApiClient.GetAllAsync(cancellationToken));

        try
        {
            await Task.WhenAll(puestosTask);
        }
        catch
        {
            // Consolidated via Task.Status below
        }

        if (puestosTask.Status == TaskStatus.RanToCompletion)
        {
            PuestoSuperiorOptions = puestosTask.Result
                .Select(PuestoFormHelpers.MapToSuperiorViewModel)
                .ToArray();
        }
        else
        {
            PuestoSuperiorOptions = [];
            anyFailure = true;
        }

        if (anyFailure)
            ErrorMessage = "No se pudo cargar el catálogo necesario. Intentá nuevamente.";
    }
}
