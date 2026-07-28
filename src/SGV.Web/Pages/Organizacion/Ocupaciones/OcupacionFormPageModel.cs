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
/// <c>_Form.cshtml</c> de Ocupaciones. Concentra la carga paralela de
/// catálogos Persona/Puesto, el mapeo de códigos de conflicto 409 al
/// <c>ModelState</c>, y la aplicación de <c>FieldErrors</c> del backend.
/// <see cref="CreateModel"/> y <see cref="EditModel"/> heredan de acá para
/// evitar duplicación de ~70 líneas por PageModel.
/// </summary>
/// <remarks>
/// Slice 3a (refactor correctivo). El nombre sigue el patrón espejo de
/// Puestos aunque estos no usen base class: la lógica de catálogos/conflictos
/// en Ocupaciones es lo suficientemente compleja para justificar el DRY.
/// </remarks>
public abstract class OcupacionFormPageModel : PageModel, IOcupacionForm
{
    /// <summary>Estado del formulario bindable.</summary>
    [BindProperty]
    public OcupacionInputModel Input { get; set; } = new();

    /// <summary>Opciones del catálogo de personas activas.</summary>
    public IReadOnlyList<PersonaDto> PersonaOptions { get; protected set; } = [];

    /// <summary>Opciones del catálogo de puestos activos.</summary>
    public IReadOnlyList<PuestoDto> PuestoOptions { get; protected set; } = [];

    /// <summary>Mensaje de error general recuperable (catálogo caído, error de transporte en POST, etc.).</summary>
    public string? ErrorMessage { get; protected set; }

    /// <summary>
    /// Carga los catálogos de personas y puestos en paralelo vía
    /// <c>Task.WhenAll</c>. Cualquier excepción de uno o más catálogos se
    /// registra con <see cref="ErrorMessage"/> y el catálogo correspondiente
    /// queda vacío. El form sigue visible para permitir reintento manual.
    /// </summary>
    /// <remarks>
    /// La falla individual se loguea con <see cref="ILogger"/> para preservar
    /// la causa raíz en producción; el <see cref="ErrorMessage"/> que ve el
    /// usuario queda como copy recuperable genérico.
    /// </remarks>
    protected async Task LoadCatalogsAsync(
        IPersonaApiClient personaApiClient,
        IPuestosApiClient puestosApiClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        var anyFailure = false;

        var personasTask = SafeAsync(() => personaApiClient.GetAllAsync(cancellationToken));
        var puestosTask = SafeAsync(() => puestosApiClient.GetAllAsync(cancellationToken));

        try
        {
            await Task.WhenAll(personasTask, puestosTask);
        }
        catch (Exception ex)
        {
            // Consolidamos por Task.Status abajo, pero dejamos traza para diagnóstico.
            logger.LogWarning(ex, "Catalog load threw before/during WhenAll.");
        }

        if (personasTask.Status == TaskStatus.RanToCompletion)
        {
            PersonaOptions = personasTask.Result;
        }
        else
        {
            PersonaOptions = [];
            anyFailure = true;
            if (personasTask.Exception is { } pex)
            {
                logger.LogWarning(pex, "Persona catalog failed to load.");
            }
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
    }

    /// <summary>
    /// Mapea los códigos de conflicto 409 al campo correspondiente del
    /// <see cref="Microsoft.AspNetCore.Mvc.ModelStateDictionary"/>.
    /// <c>PersonaYPuestoOcupados</c> muestra el mensaje en ambos campos
    /// (Persona y Puesto); <c>PuestoOcupado</c> lo muestra sólo en Puesto.
    /// Cualquier otro código de conflicto cae en un error general para no
    /// perder el feedback del backend.
    /// </summary>
    /// <remarks>
    /// Tanto Create como Edit exponen <c>PersonaId</c> como dropdown
    /// editable, así que ambos pueden disparar <c>PersonaYPuestoOcupados</c>
    /// y ambos merecen el mismo mapeo.
    /// </remarks>
    protected void MapConflictToModelState(OcupacionError error)
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