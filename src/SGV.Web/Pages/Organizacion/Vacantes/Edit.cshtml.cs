using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Vacantes;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Vacantes;

/// <summary>
/// PageModel for changing the state and observations of a vacante.
/// </summary>
[Authorize]
public sealed class EditModel(
    IVacanteApiClient vacanteApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel
{
    /// <summary>Bound edit form.</summary>
    [BindProperty]
    public VacanteEditInputModel Input { get; set; } = new();

    /// <summary>Current vacante detail shown above the form.</summary>
    public VacanteDetailViewModel? ViewModel { get; private set; }

    /// <summary>Whether the vacante cannot be loaded or edited.</summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>Available states for the edit dropdown.</summary>
    public IReadOnlyList<EstadoVacanteDto> EstadosVacante { get; private set; } = [];

    /// <summary>Whether the state catalog loaded successfully.</summary>
    public bool CatalogsReady { get; private set; }

    /// <summary>Recoverable API error.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>One-time success feedback after PRG.</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Feedback CSS kind.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>Whether the current user has a mutation role.</summary>
    public bool CanMutate => User.IsInRole(RolesSgv.Administrador) || User.IsInRole(RolesSgv.GestorVacantes);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!CanMutate)
        {
            return Forbid();
        }

        var current = await LoadCurrentAsync(id, cancellationToken);
        if (current is null)
        {
            return Page();
        }

        // Cambio vacantes-hardening F-2: guard contra vacante terminal.
        // Si la vacante ya está Cubierta/Cancelada, redirigir al Details
        // sin poblar el form. El backend rechazaría CambiarEstadoAsync
        // con 409 EstadoTerminalInmutable; lo evitamos acá.
        var viewModel = VacanteDetailViewModel.FromDto(current);
        if (viewModel.EsCerrada)
        {
            return RedirectToPage("/Organizacion/Vacantes/Details", new { id });
        }

        PopulateInput(current);
        await LoadStatesAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!CanMutate)
        {
            return Forbid();
        }

        var current = await LoadCurrentAsync(id, cancellationToken);
        if (current is null)
        {
            return Page();
        }

        ViewModel = VacanteDetailViewModel.FromDto(current);
        if (!ModelState.IsValid)
        {
            await LoadStatesAsync(cancellationToken);
            return Page();
        }

        VacanteCommandResult result;
        try
        {
            result = await vacanteApiClient.CambiarEstadoAsync(
                id,
                new CambiarEstadoVacanteRequest(
                    Input.EstadoVacanteId!.Value,
                    Normalize(Input.Motivo),
                    Normalize(Input.Observaciones)),
                cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Vacante edit transport failure for {Id}.", id);
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadStatesAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "La vacante se actualizó correctamente.");
            return RedirectToPage("/Organizacion/Vacantes/Details", new { id });
        }

        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        if (result.Error?.Categoria == ErrorCategoria.Forbidden)
        {
            return Forbid();
        }

        if (result.Error?.Categoria == ErrorCategoria.Validation
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
            ErrorMessage = result.Error is null
                ? "No se pudo actualizar la vacante."
                : ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                    conflictMessage: result.Error.Message);
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        await LoadStatesAsync(cancellationToken);
        return Page();
    }

    private async Task<VacanteDetailDto?> LoadCurrentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = await vacanteApiClient.ObtenerPorIdAsync(id, cancellationToken);
            if (current is null)
            {
                IsRecoverable = true;
                ErrorMessage = PageFeedback.NotFoundDeleteMessage;
                logger.LogWarning("Vacante with Id {Id} was not found.", id);
            }

            return current;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            IsRecoverable = true;
            ErrorMessage = PageFeedback.TransportMessage;
            logger.LogError(ex, "Failed to load vacante {Id}.", id);
            return null;
        }
    }

    private void PopulateInput(VacanteDetailDto current)
    {
        ViewModel = VacanteDetailViewModel.FromDto(current);
        Input.PuestoId = current.PuestoId;
        Input.EstadoVacanteId = current.EstadoVacanteId;
        Input.FechaApertura = current.FechaApertura;
        Input.Motivo = current.Motivo;
        Input.Observaciones = current.Observaciones;
    }

    private async Task LoadStatesAsync(CancellationToken cancellationToken)
    {
        CatalogsReady = false;
        EstadosVacante = [];
        try
        {
            EstadosVacante = (await vacanteApiClient.ListarEstadosAsync(cancellationToken))
                .Where(s => !s.EsCubierta)
                .ToList();
            CatalogsReady = true;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load vacante states catalog.");
            ErrorMessage = "No se pudo cargar el catálogo de estados. Intentá nuevamente.";
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
