using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Ocupaciones. Create (PR 3a) y Edit (PR 3a)
/// implementan la misma forma porque los cinco campos del formulario
/// (<c>PersonaId</c>, <c>PuestoId</c>, <c>FechaInicio</c>,
/// <c>TipoAsignacion</c>, <c>Observaciones</c>) son editables en ambas
/// páginas; no hay distinción <c>IsEdit</c> como en Puestos.
/// </summary>
/// <remarks>
/// Espejo de <see cref="Organizacion.IPuestoForm"/> pero sin el flag
/// <see cref="Organizacion.IPuestoForm.IsEdit"/> porque Ocupacion Create y
/// Edit exponen los mismos cinco campos (no hay campos inmutables).
/// <para>
/// Issue #216 (OCC-PER-BUSC-02): el contrato se extiende con
/// <see cref="PersonaDisplay"/> y <see cref="PersonaVinculada"/> para
/// alimentar la card enriquecida del modal reutilizable
/// (<c>_PersonaBuscadorModal</c>) cuando hay una persona precargada.
/// <see cref="PersonaOptions"/> se elimina porque el catálogo completo ya
/// no se carga: una persona puede tener múltiples ocupaciones y la
/// búsqueda del modal aplica <c>soloSinUsuario=false</c> vía
/// <c>data-solo-sin-usuario</c>.
/// </para>
/// </remarks>
public interface IOcupacionForm
{
    /// <summary>Estado del formulario bindable.</summary>
    SGV.Web.Integration.Ocupaciones.OcupacionInputModel Input { get; }

    /// <summary>Opciones del catálogo de puestos activos para popular el dropdown de <c>PuestoId</c>.</summary>
    IReadOnlyList<PuestoDto> PuestoOptions { get; }

    /// <summary>
    /// Texto visible de la persona precargada. Se formatea como
    /// <c>Apellido, Nombre (TipoDoc: NroDoc)</c> cayendo a
    /// <c>Legajo</c> cuando no hay documento. El partial lo proyecta
    /// en la card del modal. <c>null</c> en estado vacío.
    /// </summary>
    string? PersonaDisplay { get; }

    /// <summary>
    /// DTO de la persona vinculada, traído vía <c>IPersonaApiClient.GetByIdAsync</c>
    /// tras resolver <c>Input.PersonaId</c> (en Edit desde el DTO de la
    /// ocupación; en Create desde el query string <c>?personaId</c>).
    /// <c>null</c> cuando el id no existe, el API devolvió 404 o hubo
    /// fallo de transporte. Issue #216 / OCC-PER-BUSC-02.
    /// </summary>
    PersonaDto? PersonaVinculada { get; }

    /// <summary>Indica si el Puesto seleccionado carece de Vacante abierta.</summary>
    bool PuestoSinVacanteAbierta => false;

    /// <summary>Indica si el formulario corresponde al alta de una Ocupación.</summary>
    bool IsEdit { get; }

    /// <summary>Mensaje de error general recuperable (catálogo caído, error de transporte en POST, etc.).</summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// T2.6 (change <c>invertir-flujo-cubrir</c> / S2): hint informativo
    /// que se muestra en el <c>Create</c> cuando el alta proviene de
    /// <c>?vacanteId=</c> (REQ-OCC-FORM-009 invertido). Cuando es
    /// <see langword="null"/> el form asume que NO hay Vacante precargada
    /// y se renderiza el hint vigente de flujo "alta directa".
    /// </summary>
    string? VacanteHintLabel => null;

    /// <summary>
    /// T2.10 (change <c>invertir-flujo-cubrir</c> / S2): <c>true</c>
    /// cuando el dropdown de PuestoId debe renderearse bloqueado
    /// (caso <c>?vacanteId=</c> con Vacante Abierta/En Selección). El
    /// partial <c>_Form.cshtml</c> lo consume para agregar <c>disabled</c>
    /// al <c>select</c> y un <c>hidden</c> adicional que preserva el
    /// valor para el model binding. La implementación por defecto vive
    /// en <see cref="OcupacionFormPageModel.PuestoIdBloqueadoPorVacante"/>
    /// para integrarse con el binding de Razor (la vista materializa el
    /// contrato a través de la partial y los PageModels concretos).
    /// </summary>
    bool PuestoIdBloqueadoPorVacante { get; }
}
