using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Contrato compartido por los PageModels que renderizan el partial
/// <c>_Form.cshtml</c> de Puestos. Create (PR 3A) lo implementa siempre
/// con <see cref="IsEdit"/> en <c>false</c>; Edit (PR 3B) lo hará con
/// <c>true</c>. La interfaz se introduce en PR 3A para que el partial
/// shared pueda renderizar distinto sin necesidad de cambiar su
/// <c>@model</c> cuando llegue la página de Edit.
/// </summary>
public interface IPuestoForm
{
    /// <summary>Estado del formulario bindable.</summary>
    PuestoInputModel Input { get; }

    /// <summary>Opciones del catálogo de unidades organizativas para popular el dropdown de <c>UnidadOrganizativaId</c>.</summary>
    IReadOnlyList<UnidadOrganizativaDto> UnidadOrganizativaOptions { get; }

    /// <summary>Opciones del catálogo de cargos para popular el dropdown de <c>CargoId</c>.</summary>
    IReadOnlyList<CargoDto> CargoOptions { get; }

    /// <summary>
    /// Opciones del catálogo de puestos activos para popular el dropdown de
    /// <c>PuestoSuperiorId</c>. El partial <c>_Form.cshtml</c> lo renderiza
    /// con <c>new SelectList(..., "Id", "CodigoYNombre")</c>, mostrando el
    /// formato "<c>P-001 — Director</c>".
    /// </summary>
    IReadOnlyList<PuestoListItemViewModel> PuestoSuperiorOptions { get; }

    /// <summary>Mensaje de error general recuperable (catálogo caído, error de transporte en POST, etc.).</summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> cuando la página es Edit (PR 3B); el partial
    /// <c>_Form.cshtml</c> usa este flag para ocultar los campos
    /// inmutables (<c>Codigo</c>, <c>UnidadOrganizativaId</c>,
    /// <c>CargoId</c>). Create siempre devuelve <c>false</c>.
    /// </summary>
    bool IsEdit { get; }

    /// <summary>URL de retorno al listado preservando los filtros de la página anterior.</summary>
    string ReturnToListUrl { get; }
}
