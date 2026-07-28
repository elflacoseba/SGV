using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// PageModel de Edit del módulo web de Ocupaciones (Slice 3a del change
/// <c>2026-07-28-web-ocupaciones-issue-208</c>). Sólo permite editar
/// ocupaciones vigentes (<see cref="OcupacionEstado.Vigente"/>);
/// ocupaciones finalizadas o eliminadas bloquean el POST y ofrecen un
/// estado recuperable (REQ-OCC-FORM-002).
/// </summary>
/// <remarks>
/// Misma matriz de errores que <see cref="CreateModel"/>. Sobre éxito
/// redirige al detalle (PRG) preservando contexto. Sobre 409
/// <c>PuestoOcupado</c> (cuando el puesto cambió a ocupado por otro entre
/// la carga y el POST) mapea el error al campo
/// <see cref="OcupacionInputModel.PuestoId"/>.
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class EditModel(
    IOcupacionApiClient ocupacionApiClient,
    IPersonaApiClient personaApiClient,
    IPuestosApiClient puestosApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel
{
    [BindProperty]
    public OcupacionInputModel Input { get; set; } = new();

    /// <summary>DTO wire de la ocupación que se está editando.</summary>
    public OcupacionDetailsViewModel? ViewModel { get; private set; }

    public IReadOnlyList<PersonaDto> PersonaOptions { get; private set; } = [];

    public IReadOnlyList<PuestoDto> PuestoOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    /// <summary><c>true</c> cuando el recurso no está disponible para edición.</summary>
    public bool IsRecoverable { get; private set; }

    public bool IsEdit => true;

    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler. Carga la ocupación vía <see cref="IOcupacionApiClient.ObtenerPorIdAsync"/>
    /// y pre-popula los campos del form. Si el estado es
    /// <see cref="OcupacionEstado.Finalizada"/> o
    /// <see cref="OcupacionEstado.Eliminada"/>, marca <see cref="IsRecoverable"/>
    /// y bloquea el render del form (REH-OCC-FORM-002).
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OcupacionDto? current;
        try
        {
            current = await ocupacionApiClient.ObtenerPorIdAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load edit page for ocupacion {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = PageFeedback.TransportMessage;
            return Page();
        }

        if (current is null)
        {
            IsRecoverable = true;
            ErrorMessage = PageFeedback.NotFoundDeleteMessage;
            return Page();
        }

        if (current.Estado != OcupacionEstado.Vigente)
        {
            // REQ-OCC-FORM-002: bloquea edición de finalizadas/eliminadas.
            IsRecoverable = true;
            ErrorMessage = current.Estado == OcupacionEstado.Finalizada
                ? "La ocupación está finalizada y no puede editarse."
                : "La ocupación está eliminada y no puede editarse.";
            return Page();
        }

        ViewModel = OcupacionDetailsViewModel.FromDto(current);
        Input.PersonaId = current.PersonaId;
        Input.PuestoId = current.PuestoId;
        Input.FechaInicio = current.FechaInicio;
        Input.TipoAsignacion = current.TipoAsignacion;
        Input.Observaciones = current.Observaciones;

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// POST handler. Valida <c>ModelState</c>; si pasa, llama
    /// <c>PUT /api/v1/ocupaciones/{id}</c> y mapea el resultado. Sobre éxito
    /// redirige al detalle (PRG) preservando filtros; sobre 409
    /// <c>PuestoOcupado</c> mapea el error al campo PuestoId; sobre
    /// <c>Validation</c> con <c>FieldErrors</c> los aplica al
    /// <c>ModelState</c>; cualquier fallo recuperable (transporte,
    /// serialización) muestra error general y conserva input + catálogos.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Re-leemos el recurso para confirmar que sigue vigente antes de mutar.
        OcupacionDto? current;
        try
        {
            current = await ocupacionApiClient.ObtenerPorIdAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load ocupacion {Id} during POST prepopulate.", id);
            IsRecoverable = true;
            ErrorMessage = PageFeedback.TransportMessage;
            return Page();
        }

        if (current is null)
        {
            IsRecoverable = true;
            ErrorMessage = PageFeedback.NotFoundDeleteMessage;
            return Page();
        }

        if (current.Estado != OcupacionEstado.Vigente)
        {
            IsRecoverable = true;
            ErrorMessage = current.Estado == OcupacionEstado.Finalizada
                ? "La ocupación está finalizada y no puede editarse."
                : "La ocupación está eliminada y no puede editarse.";
            return Page();
        }

        ViewModel = OcupacionDetailsViewModel.FromDto(current!);

        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new ActualizarOcupacionRequest(
            Input.PersonaId!.Value,
            Input.PuestoId!.Value,
            Input.FechaInicio!.Value,
            Input.TipoAsignacion!.Value,
            string.IsNullOrWhiteSpace(Input.Observaciones) ? null : Input.Observaciones.Trim());

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.ActualizarAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion update transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            PageFeedback.SetSuccess(
                TempData,
                $"La ocupación de {result.Value.PersonaNombre} en {result.Value.PuestoNombre} se actualizó correctamente.");

            return RedirectToPage("/Organizacion/Ocupaciones/Details", new { id });
        }

        if (result.Error is not null)
        {
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

            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                switch (result.Error.Code)
                {
                    case OcupacionErrorCodigo.PersonaYPuestoOcupados:
                        ModelState.AddModelError(OcupacionFormKeys.PersonaIdKey, result.Error.Message);
                        ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, result.Error.Message);
                        break;
                    case OcupacionErrorCodigo.PuestoOcupado:
                        ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, result.Error.Message);
                        break;
                    default:
                        ErrorMessage = result.Error.Message;
                        ModelState.AddModelError(string.Empty, ErrorMessage);
                        break;
                }
            }
            else if (result.Error.Categoria == ErrorCategoria.Validation
                && result.FieldErrors is { Count: > 0 })
            {
                ApplyFieldErrors(result.FieldErrors);
            }
            else
            {
                ErrorMessage = ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                    conflictMessage: "Conflicto al actualizar la ocupación.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    private void ApplyFieldErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var entry in fieldErrors)
        {
            var key = entry.Key.StartsWith(OcupacionFormKeys.InputPrefix, StringComparison.Ordinal)
                ? entry.Key
                : OcupacionFormKeys.InputPrefix + entry.Key;
            foreach (var message in entry.Value)
            {
                ModelState.AddModelError(key, message);
            }
        }
    }

    private async Task LoadCatalogsAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        var personasTask = SafeAsync(() => personaApiClient.GetAllAsync(cancellationToken));
        var puestosTask = SafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));

        try
        {
            await Task.WhenAll(personasTask, puestosTask);
        }
        catch
        {
            // Consolidamos por Task.Status abajo.
        }

        if (personasTask.Status == TaskStatus.RanToCompletion)
        {
            PersonaOptions = personasTask.Result;
        }
        else
        {
            PersonaOptions = [];
            anyFailure = true;
        }

        if (puestosTask.Status == TaskStatus.RanToCompletion)
        {
            PuestoOptions = puestosTask.Result;
        }
        else
        {
            PuestoOptions = [];
            anyFailure = true;
        }

        if (anyFailure)
        {
            ErrorMessage = "No se pudo cargar el catálogo necesario. Intentá nuevamente.";
        }
    }

    private static async Task<T> SafeAsync<T>(Func<Task<T>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
    }
}