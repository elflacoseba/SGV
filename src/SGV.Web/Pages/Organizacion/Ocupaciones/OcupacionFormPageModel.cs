using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// Base abstracta para los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Ocupaciones. Concentra la carga del catálogo de
/// Puesto y, en Edit, el enriquecimiento opcional de la persona vinculada
/// vía <see cref="IPersonaApiClient.GetByIdAsync"/>. El catálogo completo
/// de personas (<c>PersonaOptions</c>) ya NO se carga: una persona puede
/// tener múltiples ocupaciones y la búsqueda del modal aplica
/// <c>soloSinUsuario=false</c>. Issue #216 / OCC-PER-BUSC-02.
/// <para>
/// Conserva el mapeo de códigos de conflicto 409 al
/// <c>ModelState</c> y la aplicación de <c>FieldErrors</c> del backend.
/// </para>
/// </summary>
public abstract class OcupacionFormPageModel : PageModel, IOcupacionForm
{
    /// <summary>Indica que el PageModel base representa un formulario de edición.</summary>
    public virtual bool IsEdit => true;

    /// <summary>
    /// Indica si el Puesto seleccionado carece de Vacante abierta.
    /// </summary>
    public virtual bool PuestoSinVacanteAbierta { get; protected set; }


    /// <summary>Estado del formulario bindable.</summary>
    [BindProperty]
    public OcupacionInputModel Input { get; set; } = new();

    public IReadOnlyList<PuestoDto> PuestoOptions { get; protected set; } = [];

    /// <summary>
    /// Texto visible de la persona precargada. Lo asigna el PageModel
    /// concreto tras resolver el id. Issue #216.
    /// </summary>
    public string? PersonaDisplay { get; protected set; }

    /// <summary>
    /// DTO de la persona vinculada. Lo asigna <see cref="EnriquecerPersonaAsync"/>
    /// cuando <c>Input.PersonaId</c> está resuelto. <c>null</c> cuando el
    /// id es vacío, el API devolvió 404 o falló el transporte.
    /// </summary>
    public PersonaDto? PersonaVinculada { get; protected set; }

    /// <summary>Mensaje de error general recuperable (catálogo caído, error de transporte en POST, etc.).</summary>
    public string? ErrorMessage { get; protected set; }

    /// <summary>
    /// Carga el catálogo de puestos vía <see cref="IPuestosApiClient.GetAllAsync"/>
    /// y, cuando <c>Input.PersonaId</c> está resuelto, enriquece la card
    /// llamando <see cref="EnriquecerPersonaAsync"/>. Cualquier excepción
    /// del catálogo de puestos se registra con <see cref="ErrorMessage"/>
    /// y el form queda con <c>PuestoOptions = []</c>; el enriquecimiento
    /// de persona es no-fatal (cae a card vacía sin error visible).
    /// </summary>
    protected async Task LoadCatalogsAsync(
        IPersonaApiClient personaApiClient,
        IPuestosApiClient puestosApiClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        var puestosTask = SafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));

        try
        {
            await puestosTask;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Puesto catalog load threw before/during await.");
        }

        if (puestosTask.Status == TaskStatus.RanToCompletion)
        {
            PuestoOptions = puestosTask.Result;
        }
        else
        {
            PuestoOptions = [];
            anyFailure = true;
            if (puestosTask.Exception is { } pex)
            {
                logger.LogWarning(pex, "Puesto catalog failed to load.");
            }
        }

        if (anyFailure)
        {
            ErrorMessage = "No se pudo cargar el catálogo necesario. Intentá nuevamente.";
        }

        // Enriquecimiento opcional de la persona precargada. No fatal.
        await EnriquecerPersonaAsync(personaApiClient, logger, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enriquece la card de persona cuando <see cref="Input.PersonaId"/>
    /// está resuelto. Llama <see cref="IPersonaApiClient.GetByIdAsync"/>
    /// y, sobre éxito, popula <see cref="PersonaVinculada"/> y
    /// <see cref="PersonaDisplay"/>. Sobre 404, transporte o id vacío,
    /// ambas propiedades quedan en <c>null</c> sin propagar excepción
    /// (la card cae al fallback plano).
    /// </summary>
    /// <remarks>
    /// Reemplaza la carga del catálogo completo de personas
    /// (<see cref="IPersonaApiClient.GetAllAsync"/>) que existía antes de
    /// Issue #216. El formato del display es
    /// <c>Apellido, Nombre (TipoDoc: NroDoc)</c> cayendo a <c>Legajo</c>
    /// cuando no hay documento (espejo de la función <c>personaDisplay</c>
    /// del JS compartido).
    /// </remarks>
    protected async Task EnriquecerPersonaAsync(
        IPersonaApiClient personaApiClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!Input.PersonaId.HasValue || Input.PersonaId.Value == Guid.Empty)
        {
            PersonaVinculada = null;
            PersonaDisplay = null;
            return;
        }

        try
        {
            var persona = await personaApiClient
                .GetByIdAsync(Input.PersonaId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (persona is null)
            {
                PersonaVinculada = null;
                PersonaDisplay = null;
                return;
            }

            PersonaVinculada = persona;
            PersonaDisplay = FormatearPersonaDisplay(persona);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Failed to enrich linked persona {PersonaId}; falling back to empty card.",
                Input.PersonaId.Value);
            PersonaVinculada = null;
            PersonaDisplay = null;
        }
    }

    /// <summary>
    /// Formato canónico <c>Apellido, Nombre (TipoDoc: NroDoc)</c> cayendo a
    /// <c>Legajo</c> si no hay documento. Espejo de la función
    /// <c>personaDisplay</c> del script compartido
    /// <c>usuario-persona-buscador.js</c>.
    /// </summary>
    internal static string FormatearPersonaDisplay(PersonaDto persona)
    {
        var fullName = string.Join(
            ", ",
            new[] { persona.Apellidos, persona.Nombres }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        string detail;
        if (!string.IsNullOrWhiteSpace(persona.TipoDocumentoCodigo)
            && !string.IsNullOrWhiteSpace(persona.NumeroDocumento))
        {
            detail = $"{persona.TipoDocumentoCodigo}: {persona.NumeroDocumento}";
        }
        else
        {
            detail = persona.Legajo ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(fullName)
            ? detail
            : string.IsNullOrWhiteSpace(detail)
                ? fullName
                : $"{fullName} ({detail})";
    }

    /// <summary>
    /// Mapea los códigos de conflicto 409 al campo correspondiente del
    /// <see cref="Microsoft.AspNetCore.Mvc.ModelStateDictionary"/>.
    /// <c>PersonaYPuestoOcupados</c> muestra el mensaje en ambos campos
    /// (Persona y Puesto); <c>PuestoOcupado</c> lo muestra sólo en Puesto.
    /// Cualquier otro código de conflicto cae en un error general para no
    /// perder el feedback del backend.
    /// </summary>
    protected void MapConflictToModelState(OcupacionError error)
    {
        switch (error.Code)
        {
            case OcupacionErrorCodigo.PersonaYPuestoOcupados:
                ModelState.AddModelError(OcupacionFormKeys.PersonaIdKey, error.Message);
                ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, error.Message);
                break;
            case OcupacionErrorCodigo.PuestoOcupado:
            case OcupacionErrorCodigo.PuestoSinVacanteAbierta:
                ModelState.AddModelError(OcupacionFormKeys.PuestoIdKey, error.Message);
                break;
            default:
                ErrorMessage = error.Message;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                break;
        }
    }

    /// <summary>
    /// Aplica <c>FieldErrors</c> del backend al <see cref="Microsoft.AspNetCore.Mvc.ModelStateDictionary"/>
    /// prefijando las claves con <see cref="OcupacionFormKeys.InputPrefix"/>
    /// para que <c>asp-validation-for</c> las pueda renderear al lado del
    /// campo correcto. El backend emite las claves en PascalCase
    /// (<c>PersonaId</c>, <c>PuestoId</c>, etc.); el binder de Razor espera
    /// <c>Input.PersonaId</c>, <c>Input.PuestoId</c>, etc.
    /// </summary>
    protected void ApplyFieldErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
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
    /// Wrapper para garantizar que cualquier excepción síncrona o asíncrona
    /// del factory quede en el <see cref="Task{T}"/> retornado como
    /// faulted. El keyword <c>async</c> ya hace esto automáticamente; el
    /// método existe sólo como punto único de documentación y para que
    /// <c>Task.WhenAll</c> reciba dos tareas que no fallen en su sync path.
    /// </summary>
    private static async Task<T> SafeAsync<T>(Func<Task<T>> factory)
        => await factory().ConfigureAwait(false);
}