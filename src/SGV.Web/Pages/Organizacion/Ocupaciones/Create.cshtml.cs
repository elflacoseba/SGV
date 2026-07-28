using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
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
/// PageModel de Create del módulo web de Ocupaciones (Slice 3a del
/// change <c>2026-07-28-web-ocupaciones-issue-208</c>). Carga los
/// catálogos Persona y Puesto vía <see cref="IPersonaApiClient.GetAllAsync"/>
/// y <see cref="IPuestosApiClient.GetAllAsync"/>, valida el formulario
/// y publica vía <see cref="IOcupacionApiClient.CrearAsync"/>. Sobre éxito
/// redirige al listado (PRG) preservando contexto. Sobre 409 mapea
/// <c>PersonaYPuestoOcupados</c>/<c>PuestoOcupado</c> al campo
/// correspondiente del <see cref="Microsoft.AspNetCore.Mvc.ModelStateDictionary"/>;
/// sobre <c>400</c> con <c>FieldErrors</c> aplica cada error a su campo.
/// Fallos de transporte se traducen a un error general recuperable y
/// conservan la entrada del usuario.
/// </summary>
/// <remarks>
/// <para>
/// Slice 3a: switch exhaustivo sobre <see cref="ErrorCategoria"/>.
/// <c>Unauthorized</c> redirige vía <see cref="IAuthSessionRedirector"/>;
/// <c>Forbidden</c> retorna <see cref="Forbid"/>.
/// </para>
/// <para>
/// Los códigos funcionales <c>PersonaYPuestoOcupados</c> /
/// <c>PuestoOcupado</c> del backend se preservan en
/// <see cref="OcupacionError.Code"/> y el PageModel los discrimina para
/// mapear el error al campo correcto (<see cref="OcupacionInputModel.PersonaId"/>
/// o <see cref="OcupacionInputModel.PuestoId"/>) según REQ-OCC-FORM-005.
/// </para>
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class CreateModel(
    IOcupacionApiClient ocupacionApiClient,
    IPersonaApiClient personaApiClient,
    IPuestosApiClient puestosApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IOcupacionForm
{
    [BindProperty]
    public OcupacionInputModel Input { get; set; } = new();

    /// <summary>Opciones del catálogo de personas activas.</summary>
    public IReadOnlyList<PersonaDto> PersonaOptions { get; private set; } = [];

    /// <summary>Opciones del catálogo de puestos activos.</summary>
    public IReadOnlyList<PuestoDto> PuestoOptions { get; private set; } = [];

    /// <summary>Mensaje de error visible cuando la carga inicial o el POST fallan.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Bandera estática para que la vista no muestre acciones de Edit.</summary>
    public bool IsEdit => false;

    /// <summary>Mensaje de feedback (success/warning/danger) entregado vía TempData tras PRG.</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Tipo de feedback (success/warning/danger). Por defecto <c>success</c>.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler. Carga los catálogos Persona y Puesto en paralelo vía
    /// <c>Task.WhenAll</c>. Si cualquiera falla, marca
    /// <see cref="ErrorMessage"/> con copy recuperable y los catálogos que
    /// sí llegaron se conservan; el form sigue visible para permitir
    /// reintento manual. Pre-carga <see cref="OcupacionInputModel.PersonaId"/>
    /// y <see cref="OcupacionInputModel.PuestoId"/> desde el query string
    /// (paridad con la página cruzada <c>PersonaOcupaciones</c> de Slice 3b).
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "personaId")] Guid? personaId = null,
        [FromQuery(Name = "puestoId")] Guid? puestoId = null,
        CancellationToken cancellationToken = default)
    {
        Input.PersonaId ??= personaId;
        Input.PuestoId ??= puestoId;

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// POST handler. Valida <c>ModelState</c>; si pasa, llama
    /// <c>POST /api/v1/ocupaciones</c> y mapea el resultado. Sobre éxito
    /// redirige al listado (PRG) preservando filtros; sobre 409 mapea
    /// <c>PersonaYPuestoOcupados</c>/<c>PuestoOcupado</c> al campo
    /// correspondiente; sobre 400 con <c>FieldErrors</c> los aplica al
    /// <c>ModelState</c>; cualquier fallo recuperable (transporte,
    /// serialización) muestra error general y conserva input + catálogos.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        var request = new CrearOcupacionRequest(
            Input.PersonaId!.Value,
            Input.PuestoId!.Value,
            Input.FechaInicio!.Value,
            Input.TipoAsignacion!.Value,
            string.IsNullOrWhiteSpace(Input.Observaciones) ? null : Input.Observaciones.Trim());

        OcupacionCommandResult result;
        try
        {
            result = await ocupacionApiClient.CrearAsync(request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Ocupacion create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadCatalogsAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            PageFeedback.SetSuccess(
                TempData,
                $"La ocupación de {result.Value.PersonaNombre} en {result.Value.PuestoNombre} se creó correctamente.");

            return RedirectToPage("/Organizacion/Ocupaciones/Index");
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

            if (result.Error.Categoria == ErrorCategoria.NotFound)
            {
                ErrorMessage = PageFeedback.NotFoundDeleteMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                // REQ-OCC-FORM-005: 409 debe discriminar entre
                // PersonaYPuestoOcupados (mapeo a ambos campos) y
                // PuestoOcupado (mapeo a PuestoId únicamente).
                MapConflictToModelState(result.Error);
                await LoadCatalogsAsync(cancellationToken);
                return Page();
            }

            if (result.Error.Categoria == ErrorCategoria.Validation
                && result.FieldErrors is { Count: > 0 })
            {
                ApplyFieldErrors(result.FieldErrors);
            }
            else
            {
                ErrorMessage = ErrorCategoryMapper.Map(
                    result.Error.Categoria,
                    notFoundMessage: PageFeedback.NotFoundDeleteMessage,
                    conflictMessage: "Conflicto al persistir la ocupación.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadCatalogsAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Mapea los códigos de conflicto 409 al campo correspondiente del
    /// <see cref="ModelStateDictionary"/>. <c>PersonaYPuestoOcupados</c>
    /// muestra el mensaje en ambos campos (Persona y Puesto);
    /// <c>PuestoOcupado</c> lo muestra sólo en Puesto. Cualquier otro
    /// código de conflicto cae en un error general para no perder el
    /// feedback del backend.
    /// </summary>
    private void MapConflictToModelState(OcupacionError error)
    {
        switch (error.Code)
        {
            case OcupacionErrorCodigo.PersonaYPuestoOcupados:
                ModelState.AddModelError(OcupacionFormKeys.PersonaIdKey, error.Message);
                ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, error.Message);
                break;
            case OcupacionErrorCodigo.PuestoOcupado:
                ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, error.Message);
                break;
            default:
                ErrorMessage = error.Message;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
        }
    }

    /// <summary>
    /// Aplica <c>FieldErrors</c> del backend al <see cref="ModelStateDictionary"/>
    /// prefijando las claves con <see cref="OcupacionFormKeys.InputPrefix"/>
    /// para que <c>asp-validation-for</c> las pueda renderear al lado del
    /// campo correcto. El backend emite las claves en PascalCase
    /// (<c>PersonaId</c>, <c>PuestoId</c>, etc.); el binder de Razor espera
    /// <c>Input.PersonaId</c>, <c>Input.PuestoId</c>, etc.
    /// </summary>
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

    /// <summary>
    /// Carga los catálogos de personas y puestos en paralelo vía
    /// <c>Task.WhenAll</c>. Cualquier excepción de uno o más catálogos se
    /// registra con <see cref="ErrorMessage"/> y el catálogo correspondiente
    /// queda vacío. El form sigue visible para permitir reintento manual.
    /// </summary>
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
            // Consolidamos el estado de cada catálogo por Task.Status abajo.
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

    /// <summary>
    /// Convierte throws sincrónicos en faulted tasks para que
    /// <c>Task.WhenAll</c> y los chequeos de estado puedan observar la
    /// falla de forma uniforme.
    /// </summary>
    private static async Task<T> SafeAsync<T>(Func<Task<T>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        catch
        {
            // El caller inspecciona Task.Status para distinguir éxito/falla.
            throw;
        }
    }
}