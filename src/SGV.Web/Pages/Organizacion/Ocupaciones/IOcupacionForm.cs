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
/// </remarks>
public interface IOcupacionForm
{
    /// <summary>Estado del formulario bindable.</summary>
    SGV.Web.Integration.Ocupaciones.OcupacionInputModel Input { get; }

    /// <summary>Opciones del catálogo de personas activas para popular el dropdown de <c>PersonaId</c>.</summary>
    IReadOnlyList<PersonaDto> PersonaOptions { get; }

    /// <summary>Opciones del catálogo de puestos activos para popular el dropdown de <c>PuestoId</c>.</summary>
    IReadOnlyList<PuestoDto> PuestoOptions { get; }

    /// <summary>Mensaje de error general recuperable (catálogo caído, error de transporte en POST, etc.).</summary>
    string? ErrorMessage { get; }
}