using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Vacantes;

/// <summary>
/// PageModel for creating a vacante from the Vacantes module.
/// Carga los catálogos de Puesto y Estado a través de
/// <see cref="IVacanteApiClient"/> (issue #235): la página no depende
/// de <c>IPuestosApiClient</c> cross-module, sino del método
/// <see cref="IVacanteApiClient.ListarPuestosAsync"/> declarado en el
/// propio cliente de Vacantes.
/// </summary>
[Authorize]
public sealed class CreateModel(
    IVacanteApiClient vacanteApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel
{
    /// <summary>Bound create form.</summary>
    [BindProperty]
    public VacanteInputModel Input { get; set; } = new();

    /// <summary>Available positions for the create dropdown.</summary>
    public IReadOnlyList<SGV.Contracts.Organizacion.Consultas.Dtos.PuestoDto> Puestos { get; private set; } = [];

    /// <summary>Available vacancy states for the create dropdown.</summary>
    public IReadOnlyList<SGV.Contracts.Vacantes.Consultas.Dtos.EstadoVacanteDto> EstadosVacante { get; private set; } = [];

    /// <summary>Whether both catalogs loaded successfully.</summary>
    public bool CatalogsReady { get; private set; }

    /// <summary>Recoverable catalog/API error.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>One-time success feedback after PRG.</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Feedback CSS kind.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>Whether the current user has a mutation role.</summary>
    public bool CanMutate => User.IsInRole(RolesSgv.Administrador) || User.IsInRole(RolesSgv.GestorVacantes);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        if (!CanMutate)
        {
            return Forbid();
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!CanMutate)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(Input.Motivo))
        {
            ModelState.AddModelError("Input.Motivo", "El motivo es obligatorio al crear una vacante.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        VacanteCommandResult result;
        try
        {
            result = await vacanteApiClient.CrearAsync(
                new CrearVacanteRequest(
                    Input.PuestoId!.Value,
                    Input.EstadoVacanteId!.Value,
                    Input.FechaApertura!.Value,
                    Input.Motivo!.Trim(),
                    Normalize(Input.Observaciones)),
                cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Vacante create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            PageFeedback.SetSuccess(TempData, "La vacante se creó correctamente.");
            return RedirectToPage("/Organizacion/Vacantes/Details", new { id = result.Value.Id });
        }

        await ApplyFailureAsync(result, cancellationToken);
        return Page();
    }

    private async Task ApplyFailureAsync(
        VacanteCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.Error is null)
        {
            ErrorMessage = "No se pudo crear la vacante.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return;
        }

        if (result.Error.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return;
            }
        }

        if (result.Error.Categoria == ErrorCategoria.Forbidden)
        {
            return;
        }

        if (result.Error.Categoria == ErrorCategoria.Validation
            && result.FieldErrors is { Count: > 0 })
        {
            foreach (var (key, errors) in result.FieldErrors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError($"Input.{key}", error);
                }
            }
        }
        else
        {
            ErrorMessage = ErrorCategoryMapper.Map(
                result.Error.Categoria,
                notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                conflictMessage: result.Error.Message);
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        await LoadCatalogsAsync(cancellationToken);
    }

    private async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        CatalogsReady = false;
        Puestos = [];
        EstadosVacante = [];

        try
        {
            var puestosTask = vacanteApiClient.ListarPuestosAsync(cancellationToken);
            var estadosTask = vacanteApiClient.ListarEstadosAsync(cancellationToken);
            await Task.WhenAll(puestosTask, estadosTask);
            Puestos = await puestosTask;
            EstadosVacante = await estadosTask;
            CatalogsReady = true;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load vacante create catalogs.");
            ErrorMessage = "No se pudieron cargar los catálogos. Intentá nuevamente.";
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
