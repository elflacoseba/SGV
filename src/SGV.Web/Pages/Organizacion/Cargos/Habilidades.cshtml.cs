using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel de la grilla editable de habilidades por cargo. Permite al
/// <see cref="RolesSgv.Administrador"/> listar, asignar, actualizar y quitar
/// asociaciones <c>CargoHabilidad</c>. Consume subrecurso Skills via
/// <see cref="ICargoApiClient"/> y catálogos via <see cref="IHabilidadApiClient"/>.
/// </summary>
[Authorize]
public sealed class HabilidadesModel(
    ICargoApiClient cargoApiClient,
    IHabilidadApiClient habilidadApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<HabilidadesModel> logger) : PageModel
{
    // ──────────────────────────────────────────────
    // Exposed for POST handlers in CargoHabilidadesPostHandlers
    // ──────────────────────────────────────────────

    internal ICargoApiClient CargoApiClient => cargoApiClient;
    internal IHabilidadApiClient HabilidadApiClient => habilidadApiClient;
    internal IAuthSessionRedirector AuthRedirector => authRedirector;
    internal ILogger<HabilidadesModel> Logger => logger;

    // ──────────────────────────────────────────────
    // Properties
    // ──────────────────────────────────────────────

    /// <summary>Nombre del cargo mostrado como encabezado.</summary>
    public string? CargoNombre { get; set; }

    /// <summary>Skills del cargo para la grilla editable.</summary>
    public IReadOnlyList<CargoSkillDetailDto> Skills { get; set; } = [];

    /// <summary>Catálogo de habilidades activas (dropdown "Asignar").</summary>
    public IReadOnlyList<HabilidadListItemViewModel> HabilidadesDisponibles { get; set; } = [];

    /// <summary>Catálogo de niveles (dropdown NivelRequerido).</summary>
    public IReadOnlyList<NivelHabilidadDto> NivelOptions { get; set; } = [];

    /// <summary>Input ligado al form "Asignar" — hidratado manualmente desde Request.Form.</summary>
    public CargoHabilidadAsignarInputModel AsignarInput { get; set; } = new();

    /// <summary>Mensaje de error visible en página.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Verdadero cuando el cargo no existe o falló recuperablemente.</summary>
    public bool IsRecoverable { get; set; }

    /// <summary>Mensaje de feedback desde TempData (PRG).</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Tipo de feedback (success/warning/danger).</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>Verdadero cuando el usuario autenticado es Administrador.</summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    // ──────────────────────────────────────────────
    // GET
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!EsAdministrador)
            return Forbid();

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

            CargoNombre = cargo.Nombre;
            await LoadSkillsAndCatalogsAsync(id, cancellationToken);
            return Page();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load habilidades page for cargo {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar la página de habilidades. Intentá nuevamente.";
            return Page();
        }
    }

    // ──────────────────────────────────────────────
    // POST handlers — delegan a la extracción estática
    // ──────────────────────────────────────────────

    public async Task<IActionResult> OnPostAsignarAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!EsAdministrador) return Forbid();
        return await CargoHabilidadesPostHandlers.HandleAsignarAsync(this, id, cancellationToken);
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        Guid id, Guid skillId, CancellationToken cancellationToken)
    {
        if (!EsAdministrador) return Forbid();
        return await CargoHabilidadesPostHandlers.HandleActualizarAsync(this, id, skillId, cancellationToken);
    }

    public async Task<IActionResult> OnPostQuitarAsync(
        Guid id, Guid skillId, CancellationToken cancellationToken)
    {
        if (!EsAdministrador) return Forbid();
        return await CargoHabilidadesPostHandlers.HandleQuitarAsync(this, id, skillId, cancellationToken);
    }

    // ──────────────────────────────────────────────
    // Internal helpers — reused by POST handlers via page parameter
    // ──────────────────────────────────────────────

    /// <summary>Carga skills + catálogos en paralelo.</summary>
    internal async Task LoadSkillsAndCatalogsAsync(Guid cargoId, CancellationToken cancellationToken)
    {
        try
        {
            var skillsTask = cargoApiClient.GetSkillsAsync(cargoId, cancellationToken);
            var nivelesTask = habilidadApiClient.GetNivelesHabilidadAsync(cancellationToken);
            var habilidadesTask = habilidadApiClient.GetAllAsync(cancellationToken);

            await Task.WhenAll(skillsTask, nivelesTask, habilidadesTask);

            var skills = skillsTask.Result;
            var assignedSkillIds = skills.Select(skill => skill.SkillId).ToHashSet();

            Skills = skills;
            NivelOptions = nivelesTask.Result;
            HabilidadesDisponibles = habilidadesTask.Result
                .Where(h => !assignedSkillIds.Contains(h.Id))
                .Select(h => new HabilidadListItemViewModel(
                    h.Id, h.Codigo, h.Nombre, h.Descripcion, h.Categoria))
                .ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load catalogs for habilidades page (cargoId={CargoId}).", cargoId);
            if (string.IsNullOrWhiteSpace(ErrorMessage))
                ErrorMessage = "No se pudo cargar el catálogo de habilidades o niveles.";
        }
    }

    /// <summary>Recarga datos tras un POST fallido o de validación.</summary>
    internal async Task ReloadForFailureAsync(Guid id, CancellationToken cancellationToken)
    {
        CargoNombre ??= await TryLoadCargoNombreAsync(id, cancellationToken);
        await LoadSkillsAndCatalogsAsync(id, cancellationToken);
    }

    internal async Task<string?> TryLoadCargoNombreAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var cargo = await cargoApiClient.GetByIdAsync(id, cancellationToken);
            return cargo?.Nombre;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(ex, "Failed to refresh cargo nombre during POST reload.");
            return null;
        }
    }
}
